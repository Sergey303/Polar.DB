using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Repeated-cycle regression tests for <see cref="USequence"/>.
///
/// <para>
/// These tests verify that persistence, reopen, refresh, and build operations remain
/// stable when they are repeated, not only when they are executed once.
/// </para>
///
/// <para>
/// The goal is to lock in idempotency-style behavior around the most important public
/// lifecycle and persistence operations.
/// </para>
/// </summary>
public class USequenceRepeatedCycleTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));

    /// <summary>
    /// Verifies that calling <see cref="USequence.Flush"/> twice in a row does not change
    /// the visible sequence contents and does not change the result after reopen.
    ///
    /// <para>
    /// This protects against persistence code that is correct on the first flush but
    /// mutates state or state-file interpretation on repeated flushes.
    /// </para>
    /// </summary>
    [Fact]
    public void Flush_Twice_Does_Not_Change_Visible_State_Or_Reopen_Result()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.Load(new object[]
        {
            Row(1, "Alice", 30),
            Row(2, "Bob", 40)
        });
        scope.Sequence.AppendElement(Row(3, "Carol", 50));

        var before = Ids(scope.Sequence);

        scope.Sequence.Flush();
        scope.Sequence.Flush();

        Assert.Equal(before, Ids(scope.Sequence));

        scope.Sequence.Close();
        var reopened = scope.ReopenSequence();

        Assert.Equal(before, Ids(reopened));
    }

    /// <summary>
    /// Verifies that <see cref="USequence.Refresh"/> is idempotent on already normalized state.
    ///
    /// <para>
    /// This protects against refresh logic that is correct once but keeps rewriting or
    /// shifting state on repeated refresh calls without any intervening changes.
    /// </para>
    /// </summary>
    [Fact]
    public void Refresh_Twice_On_Already_Normalized_State_Is_Idempotent()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.Load(new object[]
        {
            Row(10, "Alice", 30),
            Row(20, "Bob", 40),
            Row(30, "Carol", 50)
        });
        scope.Sequence.Flush();

        var before = Ids(scope.Sequence);

        scope.Sequence.Refresh();
        var afterFirstRefresh = Ids(scope.Sequence);

        scope.Sequence.Refresh();
        var afterSecondRefresh = Ids(scope.Sequence);

        Assert.Equal(before, afterFirstRefresh);
        Assert.Equal(before, afterSecondRefresh);
    }

    /// <summary>
    /// Verifies that repeating the close/reopen cycle preserves visible data.
    ///
    /// <para>
    /// This protects against persistence behavior that survives one restart but loses or
    /// corrupts data after repeated close/reopen cycles.
    /// </para>
    /// </summary>
    [Fact]
    public void Close_Reopen_Close_Reopen_Preserves_Visible_Data()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.Load(new object[]
        {
            Row(100, "Alice", 30),
            Row(200, "Bob", 40)
        });
        scope.Sequence.AppendElement(Row(300, "Carol", 50));
        scope.Sequence.Flush();
        scope.Sequence.Close();

        var reopened1 = scope.ReopenSequence();
        Assert.Equal(new[] { 100, 200, 300 }, Ids(reopened1));

        reopened1.Close();

        var reopened2 = scope.ReopenSequence();
        Assert.Equal(new[] { 100, 200, 300 }, Ids(reopened2));
    }

    /// <summary>
    /// Verifies that data appended after a reopen remains persisted and visible after
    /// another flush and another reopen.
    ///
    /// <para>
    /// This protects against a common storage regression where the second append cycle
    /// after restart is interpreted differently from the first append cycle.
    /// </para>
    /// </summary>
    [Fact]
    public void Append_Flush_Reopen_Append_Flush_Reopen_Preserves_All_Records_And_KeyLookups()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.Load(new object[]
        {
            Row(1, "Alice", 30),
            Row(2, "Bob", 40)
        });
        scope.Sequence.Flush();
        scope.Sequence.Close();

        var reopened1 = scope.ReopenSequence();
        reopened1.AppendElement(Row(3, "Carol", 50));
        reopened1.AppendElement(Row(4, "Dave", 60));
        reopened1.Flush();
        reopened1.Close();

        var reopened2 = scope.ReopenSequence();

        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(reopened2));
    }

    [Fact]
    public void Append_Flush_Reopen_Append_Build_Reopen_Preserves_All_Records_And_KeyLookups()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.Load(new object[]
        {
            Row(1, "Alice", 30),
            Row(2, "Bob", 40)
        });
        scope.Sequence.Flush();
        scope.Sequence.Close();

        var reopened1 = scope.ReopenSequence();
        reopened1.AppendElement(Row(3, "Carol", 50));
        reopened1.AppendElement(Row(4, "Dave", 60));
        reopened1.Build();
        reopened1.Close();

        var reopened2 = scope.ReopenSequence();

        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(reopened2));

        var byKey3 = Assert.IsType<object[]>(reopened2.GetByKey(3));
        var byKey4 = Assert.IsType<object[]>(reopened2.GetByKey(4));

        Assert.Equal("Carol", (string)byKey3[1]);
        Assert.Equal("Dave", (string)byKey4[1]);
    }
    /// <summary>
    /// Verifies that repeating the build/reopen cycle preserves both primary-key lookup
    /// and secondary-index lookup consistency.
    ///
    /// <para>
    /// This protects the repository contract that build and persisted state should remain
    /// stable across repeated cycles rather than only after the first build.
    /// </para>
    /// </summary>
    [Fact]
    public void Build_Reopen_Build_Reopen_Preserves_Key_And_Secondary_Index_Consistency()
    {
        using var scope = new USequenceScope(PersonType, withAgeIndex: true);

        scope.Sequence.Load(new object[]
        {
            Row(1, "Alice", 30),
            Row(2, "Bob", 30),
            Row(3, "Carol", 40)
        });
        scope.Sequence.Build();
        scope.Sequence.Close();

        var reopened1 = scope.ReopenSequence();
        reopened1.Refresh();

        AssertPrimaryKeyLookup(reopened1, 2, "Bob");
        AssertAgeLookup(reopened1, 30, new[] { 1, 2 });

        reopened1.Build();
        reopened1.Close();

        var reopened2 = scope.ReopenSequence();
        reopened2.Refresh();

        AssertPrimaryKeyLookup(reopened2, 2, "Bob");
        AssertAgeLookup(reopened2, 30, new[] { 1, 2 });
    }

    private static object[] Row(int id, string name, int age) => new object[] { id, name, age };

    private static int[] Ids(USequence sequence) =>
        sequence.ElementValues().Cast<object[]>().Select(r => (int)r[0]).ToArray();

    private static void AssertPrimaryKeyLookup(USequence sequence, int id, string expectedName)
    {
        var record = Assert.IsType<object[]>(sequence.GetByKey(id));
        Assert.Equal(id, (int)record[0]);
        Assert.Equal(expectedName, (string)record[1]);
    }

    private static void AssertAgeLookup(USequence sequence, int age, int[] expectedIds)
    {
        var resultIds = sequence
            .GetAllByValue(0, age, _ => Array.Empty<IComparable>())
            .Cast<object[]>()
            .Select(r => (int)r[0])
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(expectedIds, resultIds);
    }

    private sealed class USequenceScope : IDisposable
    {
        private readonly PType _type;
        private readonly bool _withAgeIndex;
        private readonly string _tempDir;
        private int _fileNo;

        public USequenceScope(PType type, bool withAgeIndex = false)
        {
            _type = type;
            _withAgeIndex = withAgeIndex;
            _tempDir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            StateFilePath = Path.Combine(_tempDir, "state.bin");
            Sequence = CreateSequence();
        }

        public USequence Sequence { get; private set; }
        public string StateFilePath { get; }

        public USequence ReopenSequence()
        {
            _fileNo = 0;
            Sequence = CreateSequence();
            return Sequence;
        }

        private USequence CreateSequence()
        {
            var sequence = new USequence(
                _type,
                StateFilePath,
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);

            if (_withAgeIndex)
            {
                var ageIndex = new UVectorIndex(
                    StreamGen,
                    sequence,
                    new PType(PTypeEnumeration.integer),
                    record => new IComparable[] { (int)((object[])record)[2] });

                sequence.uindexes = new IUIndex[] { ageIndex };
            }

            return sequence;
        }

        private Stream StreamGen()
        {
            var path = Path.Combine(_tempDir, $"f{_fileNo++}.bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public void Dispose()
        {
            try { Sequence.Close(); } catch { }
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
    }
}
