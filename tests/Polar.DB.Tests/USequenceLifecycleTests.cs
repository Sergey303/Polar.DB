using System.Reflection;
using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Lifecycle-oriented tests for <see cref="USequence"/>.
///
/// These tests focus on the public facade rather than on the already well-covered
/// low-level storage primitive. The goal is to lock down repository-level behavior:
/// loading, state persistence, dynamic recovery, duplicate shadowing, tombstones,
/// and explicit reindexing through <see cref="USequence.CorrectOnAppendElement(long)"/>.
/// </summary>
public class USequenceLifecycleTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    /// <summary>
    /// Verifies that <see cref="USequence.Load"/> respects the configured
    /// emptiness predicate and does not persist tombstone-like elements.
    /// </summary>
    [Fact]
    public void Load_SkipsEmptyElements_And_ElementValues_ReturnOnlyNonEmptyRecords()
    {
        using var scope = new SequenceScope(deleteOnDispose: true);

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE" },
            new object[] { 2, "" },
            new object[] { 3, "BOB" }
        });

        var ids = scope.Sequence.ElementValues()
            .Cast<object[]>()
            .Select(r => (int)r[0])
            .OrderBy(v => v)
            .ToArray();

        Assert.Equal(new[] { 1, 3 }, ids);
    }

    /// <summary>
    /// Verifies that <see cref="USequence.Build"/> stores the current count and
    /// logical append offset in the external state file.
    /// </summary>
    [Fact]
    public void Build_WritesStateFile_WithCurrentCountAndLogicalTail()
    {
        using var scope = new SequenceScope(deleteOnDispose: true);

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE" },
            new object[] { 2, "BOB" }
        });
        scope.Sequence.Build();

        using var state = new FileStream(scope.StateFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(state);

        long count = reader.ReadInt64();
        long appendOffset = reader.ReadInt64();

        Assert.Equal(2L, count);
        Assert.True(appendOffset > 8L);
    }

    /// <summary>
    /// Verifies that <see cref="USequence.RestoreDynamic"/> can rebuild dynamic
    /// primary and secondary indexes from the current sequence tail after restart.
    /// </summary>
    [Fact]
    public void RestoreDynamic_ReplaysDynamicTail_AfterRestart()
    {
        string tempDir = SequenceScope.CreateTempDirectory();

        try
        {
            using (var writerScope = new SequenceScope(tempDir, deleteOnDispose: false, withNameIndex: true))
            {
                writerScope.Sequence.Load(new object[]
                {
                    new object[] { 1, "ALICE" }
                });
                writerScope.Sequence.Build();
                writerScope.Sequence.AppendElement(new object[] { 2, "BOB" });
            }

            using var readerScope = new SequenceScope(tempDir, deleteOnDispose: true, withNameIndex: true);

            readerScope.Sequence.RestoreDynamic();

            var byKey = Assert.IsType<object[]>(readerScope.Sequence.GetByKey(2));
            Assert.Equal("BOB", (string)byKey[1]);

            var byName = readerScope.Sequence
                .GetAllByValue(0, "BOB", _ => Array.Empty<IComparable>())
                .Cast<object[]>()
                .ToArray();

            Assert.Single(byName);
            Assert.Equal(2, (int)byName[0][0]);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// Verifies the public visibility rules of the facade:
    /// the latest duplicate key shadows the old value, and an empty replacement
    /// behaves like a tombstone that hides both the old and the new record.
    /// </summary>
    [Fact]
    public void ElementValues_And_Scan_HideShadowedAndEmptyEntries()
    {
        using var scope = new SequenceScope(deleteOnDispose: true);

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE" },
            new object[] { 2, "BOB" }
        });
        scope.Sequence.Build();

        scope.Sequence.AppendElement(new object[] { 2, "BOB v2" });
        scope.Sequence.AppendElement(new object[] { 1, "" });

        var values = scope.Sequence.ElementValues()
            .Cast<object[]>()
            .Select(r => ((int)r[0], (string)r[1]))
            .ToArray();

        Assert.Single(values);
        Assert.Equal((2, "BOB v2"), values[0]);

        var scanned = new List<(int id, string name)>();
        scope.Sequence.Scan((_, obj) =>
        {
            var r = (object[])obj;
            scanned.Add(((int)r[0], (string)r[1]));
            return true;
        });

        Assert.Equal(values, scanned.ToArray());
    }

    /// <summary>
    /// Verifies the explicit recovery hook for externally appended data:
    /// after a raw append performed through the underlying sequence,
    /// <see cref="USequence.CorrectOnAppendElement(long)"/> must update indexes.
    /// </summary>
    [Fact]
    public void CorrectOnAppendElement_IndexesExternallyAppendedTailRecord()
    {
        using var scope = new SequenceScope(deleteOnDispose: true);

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE" }
        });
        scope.Sequence.Build();

        var rawSequence = GetInnerSequence(scope.Sequence);
        long appendedOffset = rawSequence.AppendElement(new object[] { 2, "BOB" });
        rawSequence.Flush();

        Assert.Null(scope.Sequence.GetByKey(2));

        scope.Sequence.CorrectOnAppendElement(appendedOffset);

        var restored = Assert.IsType<object[]>(scope.Sequence.GetByKey(2));
        Assert.Equal("BOB", (string)restored[1]);
    }

    /// <summary>
    /// Reads the private storage sequence used by the facade.
    /// This is intentionally test-only: it lets the public recovery hook be
    /// exercised against a realistic external append scenario.
    /// </summary>
    private static UniversalSequenceBase GetInnerSequence(USequence sequence)
    {
        var field = typeof(USequence).GetField("sequence", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return Assert.IsType<UniversalSequenceBase>(field.GetValue(sequence));
    }

    /// <summary>
    /// Best-effort cleanup helper for temporary test directories.
    /// </summary>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignore cleanup failures in tests
        }
    }

    /// <summary>
    /// Disk-backed scope with deterministic stream allocation order, which makes
    /// restart tests possible without hard-coding concrete index file names.
    /// </summary>
    private sealed class SequenceScope : IDisposable
    {
        private int _fileNo;
        private readonly bool _deleteOnDispose;

        public SequenceScope(bool deleteOnDispose, bool withNameIndex = false)
            : this(CreateTempDirectory(), deleteOnDispose, withNameIndex)
        {
        }

        public SequenceScope(string tempDir, bool deleteOnDispose, bool withNameIndex = false)
        {
            TempDir = tempDir;
            Directory.CreateDirectory(TempDir);
            _deleteOnDispose = deleteOnDispose;

            Sequence = new USequence(
                PersonType,
                StateFilePath,
                StreamGen,
                IsEmpty,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);

            if (withNameIndex)
            {
                Sequence.uindexes = new IUIndex[]
                {
                    new SVectorIndex(StreamGen, Sequence, r => new[] { (string)((object[])r)[1] }, ignorecase: true)
                };
            }
        }

        public string TempDir { get; }
        public string StateFilePath => Path.Combine(TempDir, "state.bin");
        public USequence Sequence { get; }

        public static string CreateTempDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
        }

        private static bool IsEmpty(object value)
        {
            return string.IsNullOrEmpty((string)((object[])value)[1]);
        }

        private Stream StreamGen()
        {
            return new FileStream(
                Path.Combine(TempDir, $"f{_fileNo++}.bin"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
        }

        public void Dispose()
        {
            try { Sequence.Close(); }
            catch
            {
                // ignored
            }

            if (_deleteOnDispose)
                TryDeleteDirectory(TempDir);
        }
    }
}
