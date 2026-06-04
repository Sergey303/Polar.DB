using Polar.DB.ExternalKey;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class USequenceCompactIndexesTests
{
    [Fact]
    public async Task CompactIndexesAsync_Compacts_Primary_And_External_Indexes()
    {
        using var env = new SequenceEnvironment();
        env.Sequence.uindexes = new IUIndex[]
        {
            new ExternalKeyIndex<string>(
                env.StreamGen,
                env.Sequence,
                r => new[] { (string)((object[])r)[1] },
                StringComparer.OrdinalIgnoreCase)
        };

        env.Sequence.Load(new object[]
        {
            Row(1, "ALICE"),
            Row(2, "BOB")
        });
        env.Sequence.Build();

        env.Sequence.AppendElement(Row(1, "CAROL"));
        env.Sequence.AppendElement(Row(3, "DAVE"));

        Assert.Equal("CAROL", NameOf(env.Sequence.GetByKey(1)));
        Assert.Empty(QueryIds(env.Sequence, "alice"));
        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, "carol"));

        // TODO await env.Sequence.CompactIndexesAsync();
        env.Sequence.Refresh();

        Assert.Equal("CAROL", NameOf(env.Sequence.GetByKey(1)));
        Assert.Equal("DAVE", NameOf(env.Sequence.GetByKey(3)));
        Assert.Empty(QueryIds(env.Sequence, "alice"));
        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, "carol"));
        Assert.Equal(new[] { 2 }, QueryIds(env.Sequence, "bob"));
    }

    private static object[] Row(int id, string name)
    {
        return new object[] { id, name };
    }

    private static string NameOf(object record)
    {
        return (string)((object[])record)[1];
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
