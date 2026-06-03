using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class SecondaryIndexesTests
{
    private static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)),
        new NamedType("tags", new PTypeSequence(new PType(PTypeEnumeration.sstring))));

    [Fact]
    public void SVector_And_UIndex_Return_Expected_Results_AfterBuild()
    {
        using var scope = new IndexedSequenceScope();

        var sIndex = new SVectorIndex(scope.StreamGen, scope.Sequence, r => new[] { (string)((object[])r)[1] });
        var exactNameIndex = new UIndex(
            scope.StreamGen,
            scope.Sequence,
            applicable: _ => true,
            hashFunc: r => Hashfunctions.HashRot13((string)((object[])r)[1]),
            comp: Comparer<object>.Create((a, b) => string.Compare((string)((object[])a)[1], (string)((object[])b)[1], StringComparison.Ordinal)));

        scope.Sequence.uindexes = new IUIndex[] { sIndex, exactNameIndex };

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE", 30, new object[] { "news", "tech" } },
            new object[] { 2, "BOB", 40, new object[] { "sports" } },
            new object[] { 3, "BOB", 30, new object[] { "news" } }
        });
        scope.Sequence.Build();

        var byName = scope.Sequence.GetAllByValue(0, "alice", _ => Array.Empty<IComparable>()).Cast<object[]>().ToArray();
        Assert.Single(byName);
        Assert.Equal(1, (int)byName[0][0]);

        var byLike = scope.Sequence.GetAllByLike(0, "AL").Cast<object[]>().ToArray();
        Assert.Single(byLike);
        Assert.Equal(1, (int)byLike[0][0]);

        var sample = new object[] { 0, "BOB", 0, Array.Empty<object>() };
        var bySample = scope.Sequence.GetAllBySample(1, sample).Cast<object[]>().ToArray();
        Assert.Equal(new[] { 2, 3 }, bySample.Select(r => (int)r[0]).OrderBy(x => x).ToArray());

        var duplicateName = new object[] { 0, "ALICE", 0, Array.Empty<object>() };
        var allBySameName = scope.Sequence.GetAllBySample(1, duplicateName).Cast<object[]>().ToArray();
        Assert.Equal(new[] { 1 }, allBySameName.Select(r => (int)r[0]).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void SVector_Sees_Dynamic_Appends_Without_Rebuild()
    {
        using var scope = new IndexedSequenceScope();

        var sIndex = new SVectorIndex(scope.StreamGen, scope.Sequence, r => new[] { (string)((object[])r)[1] });
        scope.Sequence.uindexes = new IUIndex[] { sIndex };

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE", 30, new object[] { "news" } }
        });
        scope.Sequence.Build();

        scope.Sequence.AppendElement(new object[] { 2, "BOB", 35, new object[] { "sport", "news" } });

        var byName = scope.Sequence.GetAllByValue(0, "bob", _ => Array.Empty<IComparable>()).Cast<object[]>().ToArray();
        Assert.Single(byName);
        Assert.Equal(2, (int)byName[0][0]);
    }

    private sealed class IndexedSequenceScope : IDisposable
    {
        private readonly string _tempDir;
        private int _fileNo;

        public IndexedSequenceScope()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            Sequence = new USequence(
                RecordType,
                Path.Combine(_tempDir, "state.bin"),
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);
        }

        public USequence Sequence { get; }

        public Stream StreamGen()
        {
            return new FileStream(
                Path.Combine(_tempDir, $"f{_fileNo++}.bin"),
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

            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignored
            }
        }
    }
}
