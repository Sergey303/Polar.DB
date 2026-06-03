using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Lifecycle-heavy integration tests for the supported secondary index family.
/// </summary>
public class SecondaryIndexesLifecycleTests
{
    private static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)),
        new NamedType("tags", new PTypeSequence(new PType(PTypeEnumeration.sstring))));

    [Fact]
    public void Refresh_AfterReopen_PreservesQueriesAcrossConfiguredSecondaryIndexes()
    {
        string tempDir = IndexedSequenceScope.CreateTempDirectory();

        try
        {
            using (var writerScope = new IndexedSequenceScope(tempDir, deleteOnDispose: false))
            {
                writerScope.Sequence.Load(new object[]
                {
                    new object[] { 1, "ALICE", 30, new object[] { "news", "tech" } },
                    new object[] { 2, "BOB", 40, new object[] { "sports" } },
                    new object[] { 3, "ANNA", 30, new object[] { "news" } }
                });
                writerScope.Sequence.Build();
            }

            using var readerScope = new IndexedSequenceScope(tempDir, deleteOnDispose: true);
            readerScope.Sequence.Refresh();

            var byName = readerScope.Sequence.GetAllByValue(0, "alice", _ => Array.Empty<IComparable>())
                .Cast<object[]>()
                .ToArray();
            Assert.Single(byName);
            Assert.Equal(1, (int)byName[0][0]);

            var bySample = readerScope.Sequence.GetAllBySample(1, new object[] { 0, "BOB", 0, Array.Empty<object>() })
                .Cast<object[]>()
                .ToArray();
            Assert.Single(bySample);
            Assert.Equal(2, (int)bySample[0][0]);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void Clear_RemovesStaticAndDynamicHits_FromAllConfiguredSecondaryIndexes()
    {
        using var scope = new IndexedSequenceScope(deleteOnDispose: true);

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE", 30, new object[] { "news" } }
        });
        scope.Sequence.Build();
        scope.Sequence.AppendElement(new object[] { 2, "BOB", 35, new object[] { "sport", "news" } });

        Assert.NotEmpty(scope.Sequence.GetAllByValue(0, "alice", _ => Array.Empty<IComparable>()));
        Assert.NotEmpty(scope.Sequence.GetAllBySample(1, new object[] { 0, "BOB", 0, Array.Empty<object>() }));

        scope.Sequence.Clear();

        Assert.Empty(scope.Sequence.GetAllByValue(0, "alice", _ => Array.Empty<IComparable>()));
        Assert.Empty(scope.Sequence.GetAllBySample(1, new object[] { 0, "BOB", 0, Array.Empty<object>() }));
    }

    [Fact]
    public void RestoreDynamic_AfterReopen_ReplaysSecondaryIndexesForTailAppends()
    {
        string tempDir = IndexedSequenceScope.CreateTempDirectory();

        try
        {
            using (var writerScope = new IndexedSequenceScope(tempDir, deleteOnDispose: false))
            {
                writerScope.Sequence.Load(new object[]
                {
                    new object[] { 1, "ALICE", 30, new object[] { "news" } }
                });
                writerScope.Sequence.Build();
                writerScope.Sequence.AppendElement(new object[] { 2, "BOB", 35, new object[] { "sport", "news" } });
            }

            using var readerScope = new IndexedSequenceScope(tempDir, deleteOnDispose: true);
            readerScope.Sequence.RestoreDynamic();

            var byName = readerScope.Sequence.GetAllByValue(0, "BOB", _ => Array.Empty<IComparable>())
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

    private sealed class IndexedSequenceScope : IDisposable
    {
        private int _fileNo;
        private readonly bool _deleteOnDispose;

        public IndexedSequenceScope(bool deleteOnDispose)
            : this(CreateTempDirectory(), deleteOnDispose)
        {
        }

        public IndexedSequenceScope(string tempDir, bool deleteOnDispose)
        {
            TempDir = tempDir;
            Directory.CreateDirectory(TempDir);
            _deleteOnDispose = deleteOnDispose;

            Sequence = new USequence(
                RecordType,
                Path.Combine(TempDir, "state.bin"),
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);

            var sIndex = new SVectorIndex(StreamGen, Sequence, r => new[] { (string)((object[])r)[1] }, ignorecase: true);
            var exactNameIndex = new UIndex(
                StreamGen,
                Sequence,
                applicable: _ => true,
                hashFunc: r => Hashfunctions.HashRot13((string)((object[])r)[1]),
                comp: Comparer<object>.Create((a, b) =>
                    string.Compare((string)((object[])a)[1], (string)((object[])b)[1], StringComparison.Ordinal)));

            Sequence.uindexes = new IUIndex[] { sIndex, exactNameIndex };
        }

        public string TempDir { get; }
        public USequence Sequence { get; }

        public static string CreateTempDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
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
