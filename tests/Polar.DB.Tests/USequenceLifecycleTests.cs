using Xunit;

namespace Polar.DB.Tests;

public class USequenceLifecycleTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    [Fact]
    public void Clear_Resets_Visible_Sequence_State_And_Keeps_Object_Reusable()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.AppendElement(new object[] { 1, "Alice" });
        scope.Sequence.AppendElement(new object[] { 2, "Bob" });

        scope.Sequence.Clear();

        Assert.Empty(scope.Sequence.ElementValues());

        scope.Sequence.AppendElement(new object[] { 3, "Carol" });

        var values = scope.Sequence.ElementValues().Cast<object[]>().ToArray();
        Assert.Single(values);
        Assert.Equal(3, (int)values[0][0]);
        Assert.Equal("Carol", (string)values[0][1]);
    }

    [Fact]
    public void Flush_Persists_Current_State_File_Without_Build()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "Alice" },
            new object[] { 2, "Bob" }
        });

        scope.Sequence.AppendElement(new object[] { 3, "Carol" });
        scope.Sequence.Flush();

        var values = scope.Sequence.ElementValues().Cast<object[]>().Select(r => (int)r[0]).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, values);

        var reopened = scope.ReopenSequence();
        Assert.Equal(new[] { 1, 2, 3 }, reopened.ElementValues().Cast<object[]>().Select(r => (int)r[0]).ToArray());
    }

    [Fact]
    public void Close_Flushes_State_And_Reopen_Remains_Consistent()
    {
        using var scope = new USequenceScope(PersonType);

        scope.Sequence.Load(new object[]
        {
            new object[] { 10, "Alice" },
            new object[] { 20, "Bob" }
        });
        scope.Sequence.AppendElement(new object[] { 30, "Carol" });
        scope.Sequence.Flush();

        scope.Sequence.Close();

        var reopened = scope.ReopenSequence();
        Assert.Equal(3, reopened.ElementValues().Cast<object[]>().Count());
        Assert.Equal(new[] { 10, 20, 30 }, reopened.ElementValues().Cast<object[]>().Select(r => (int)r[0]).ToArray());
    }

    private sealed class USequenceScope : IDisposable
    {
        private readonly PType _type;
        private readonly string _tempDir;
        private int _fileNo;

        public USequenceScope(PType type)
        {
            _type = type;
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
            return new USequence(
                _type,
                StateFilePath,
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);
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
