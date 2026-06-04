using Polar.DB.ExternalKey;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class ExternalKeyIndexCompactTests
{
    [Fact]
    public async Task CompactAsync_Publishes_Compacted_Static_Snapshot()
    {
        using var env = new SequenceEnvironment();
        var index = new ExternalKeyIndex<string>(
            env.StreamGen,
            env.Sequence,
            r => new[] { (string)((object[])r)[1] },
            StringComparer.OrdinalIgnoreCase);

        env.Sequence.uindexes = new IUIndex[] { index };

        env.Sequence.Load(new object[]
        {
            Row(1, "ALICE"),
            Row(2, "BOB")
        });
        env.Sequence.Build();

        env.Sequence.AppendElement(Row(1, "CAROL"));

        Assert.Empty(QueryIds(env.Sequence, "alice"));
        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, "carol"));

        await index.CompactAsync();
        index.Refresh();

        Assert.Empty(QueryIds(env.Sequence, "alice"));
        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, "carol"));
        Assert.Equal(new[] { 2 }, QueryIds(env.Sequence, "bob"));
    }

    [Fact]
    public async Task CompactAsync_Keeps_Appends_Made_While_Compact_Is_Running()
    {
        using var env = new SequenceEnvironment();
        var index = new ExternalKeyIndex<string>(
            env.StreamGen,
            env.Sequence,
            r => new[] { (string)((object[])r)[1] },
            StringComparer.OrdinalIgnoreCase);

        env.Sequence.uindexes = new IUIndex[] { index };
        env.Sequence.Load(new object[] { Row(1, "ALICE") });
        env.Sequence.Build();

        Task compact = index.CompactAsync();
        env.Sequence.AppendElement(Row(2, "BOB"));
        await compact;

        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, "alice"));
        Assert.Equal(new[] { 2 }, QueryIds(env.Sequence, "bob"));
    }

    private static object[] Row(int id, string name)
    {
        return new object[] { id, name };
    }

    private static int[] QueryIds(USequence sequence, string key)
    {
        return sequence.GetAllByValue(0, key, _ => Array.Empty<IComparable>())
            .Cast<object[]>()
            .Select(r => (int)r[0])
            .OrderBy(id => id)
            .ToArray();
    }

    private sealed class SequenceEnvironment : IDisposable
    {
        private readonly List<Stream> _streams = new();

        public SequenceEnvironment()
        {
            Sequence = new USequence(
                new PTypeRecord(
                    new NamedType("id", new PType(PTypeEnumeration.integer)),
                    new NamedType("name", new PType(PTypeEnumeration.sstring))),
                null,
                StreamGen,
                record => string.IsNullOrEmpty((string)((object[])record)[1]),
                record => (int)((object[])record)[0],
                key => (int)key,
                optimise: false);
        }

        public USequence Sequence { get; }

        public Stream StreamGen()
        {
            var stream = new MemoryStream();
            _streams.Add(stream);
            return stream;
        }

        public void Dispose()
        {
            Sequence.Close();
            foreach (var stream in _streams) stream.Dispose();
        }
    }
}
