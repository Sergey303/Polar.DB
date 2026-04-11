using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Lifecycle-heavy integration tests for the secondary index family.
///
/// The focus here is not the isolated query algorithm itself, but the operational
/// behavior that matters in the repository: clear, restart/refresh, collision-heavy
/// search, and replay of dynamic appends after reopening the sequence.
/// </summary>
public class SecondaryIndexesLifecycleTests
{
    private static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)),
        new NamedType("tags", new PTypeSequence(new PType(PTypeEnumeration.sstring))));

    /// <summary>
    /// Verifies that static index files survive restart and can be queried again
    /// after a clean <see cref="USequence.Refresh"/> on a reopened instance.
    /// </summary>
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

            var byAge = readerScope.Sequence.GetAllByValue(1, 30, _ => Array.Empty<IComparable>())
                .Cast<object[]>()
                .Select(r => (int)r[0])
                .OrderBy(v => v)
                .ToArray();
            Assert.Equal(new[] { 1, 3 }, byAge);

            var byTag = readerScope.Sequence.GetAllByValue(2, "NEWS", TagsOf, ignorecase: true)
                .Cast<object[]>()
                .Select(r => (int)r[0])
                .OrderBy(v => v)
                .ToArray();
            Assert.Equal(new[] { 1, 3 }, byTag);

            var bySample = readerScope.Sequence.GetAllBySample(3, new object[] { 0, "BOB", 0, Array.Empty<object>() })
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

    /// <summary>
    /// Verifies that clearing the facade clears both the persisted static parts
    /// and the dynamic append-only parts of all configured secondary indexes.
    /// </summary>
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
        Assert.NotEmpty(scope.Sequence.GetAllByValue(1, 35, _ => Array.Empty<IComparable>()));
        Assert.NotEmpty(scope.Sequence.GetAllByValue(2, "NEWS", TagsOf, ignorecase: true));

        scope.Sequence.Clear();

        Assert.Empty(scope.Sequence.GetAllByValue(0, "alice", _ => Array.Empty<IComparable>()));
        Assert.Empty(scope.Sequence.GetAllByValue(1, 35, _ => Array.Empty<IComparable>()));
        Assert.Empty(scope.Sequence.GetAllByValue(2, "NEWS", TagsOf, ignorecase: true));
        Assert.Empty(scope.Sequence.GetAllBySample(3, new object[] { 0, "BOB", 0, Array.Empty<object>() }));
    }

    /// <summary>
    /// Verifies that a collision-heavy <see cref="UVecIndex"/> still returns only
    /// real semantic matches because the facade performs exact post-filtering.
    /// </summary>
    [Fact]
    public void UVecIndex_WithHeavyHashCollisions_ReturnsOnlyRealMatches()
    {
        using var scope = new IndexedSequenceScope(deleteOnDispose: true, tagHash: _ => 0);

        scope.Sequence.Load(new object[]
        {
            new object[] { 1, "ALICE", 30, new object[] { "news" } },
            new object[] { 2, "BOB", 40, new object[] { "sports" } },
            new object[] { 3, "ANNA", 35, new object[] { "world", "NEWS" } }
        });
        scope.Sequence.Build();

        var byTag = scope.Sequence.GetAllByValue(2, "news", TagsOf, ignorecase: true)
            .Cast<object[]>()
            .Select(r => (int)r[0])
            .OrderBy(v => v)
            .ToArray();

        Assert.Equal(new[] { 1, 3 }, byTag);
    }

    /// <summary>
    /// Verifies that dynamic appends made after <see cref="USequence.Build"/> can be
    /// recovered into all configured secondary indexes after restart by replaying
    /// only the tail that was not yet present in the saved state file.
    /// </summary>
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

            var byAge = readerScope.Sequence.GetAllByValue(1, 35, _ => Array.Empty<IComparable>())
                .Cast<object[]>()
                .ToArray();
            Assert.Single(byAge);
            Assert.Equal(2, (int)byAge[0][0]);

            var byTag = readerScope.Sequence.GetAllByValue(2, "NEWS", TagsOf, ignorecase: true)
                .Cast<object[]>()
                .Select(r => (int)r[0])
                .OrderBy(v => v)
                .ToArray();
            Assert.Equal(new[] { 1, 2 }, byTag);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// Extracts comparable tag values from the record shape used in the test suite.
    /// </summary>
    private static IEnumerable<IComparable> TagsOf(object record)
    {
        return ((object[])((object[])record)[3]).Cast<IComparable>();
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
    /// Deterministic disk-backed scope for secondary index lifecycle tests.
    /// </summary>
    private sealed class IndexedSequenceScope : IDisposable
    {
        private int _fileNo;
        private readonly bool _deleteOnDispose;
        private readonly Func<IComparable, int> _tagHash;

        public IndexedSequenceScope(bool deleteOnDispose, Func<IComparable, int>? tagHash = null)
            : this(CreateTempDirectory(), deleteOnDispose, tagHash)
        {
        }

        public IndexedSequenceScope(string tempDir, bool deleteOnDispose, Func<IComparable, int>? tagHash = null)
        {
            TempDir = tempDir;
            Directory.CreateDirectory(TempDir);
            _deleteOnDispose = deleteOnDispose;
            _tagHash = tagHash ?? (tag => Hashfunctions.HashRot13((string)tag));

            Sequence = new USequence(
                RecordType,
                Path.Combine(TempDir, "state.bin"),
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);

            var sIndex = new SVectorIndex(StreamGen, Sequence, r => new[] { (string)((object[])r)[1] }, ignorecase: true);
            var ageIndex = new UVectorIndex(StreamGen, Sequence, new PType(PTypeEnumeration.integer), r => new IComparable[] { (int)((object[])r)[2] });
            var tagIndex = new UVecIndex(StreamGen, Sequence, TagsOf, _tagHash, ignorecase: true);
            var exactNameIndex = new UIndex(
                StreamGen,
                Sequence,
                applicable: _ => true,
                hashFunc: r => Hashfunctions.HashRot13((string)((object[])r)[1]),
                comp: Comparer<object>.Create((a, b) =>
                    string.Compare((string)((object[])a)[1], (string)((object[])b)[1], StringComparison.Ordinal)));

            Sequence.uindexes = new IUIndex[] { sIndex, ageIndex, tagIndex, exactNameIndex };
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
            try { Sequence.Close(); } catch { }

            if (_deleteOnDispose)
                TryDeleteDirectory(TempDir);
        }
    }
}
