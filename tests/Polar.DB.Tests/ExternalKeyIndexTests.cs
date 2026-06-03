using Polar.Universal;
using Polar.DB.ExternalKey;
using Xunit;

namespace Polar.DB.Tests;

public class ExternalKeyIndexTests
{
    private static readonly Guid FirstGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void GetAllByValue_Returns_Matches_For_Supported_Key_Types()
    {
        using var env = new SequenceEnvironment();
        env.Sequence.uindexes = new IUIndex[]
        {
            new ExternalKeyIndex<int>(env.StreamGen, env.Sequence, r => new[] { (int)((object[])r)[0] }),
            new ExternalKeyIndex<long>(env.StreamGen, env.Sequence, r => new[] { (long)((object[])r)[2] }),
            new ExternalKeyIndex<string>(env.StreamGen, env.Sequence, r => new[] { (string)((object[])r)[1] }),
            new ExternalKeyIndex<Guid>(env.StreamGen, env.Sequence, r => new[] { Guid.Parse((string)((object[])r)[3]) })
        };

        env.Sequence.Load(new object[]
        {
            Row(1, "ALICE", 100L, FirstGuid),
            Row(2, "BOB", 200L, SecondGuid),
            Row(3, "BOB", 100L, FirstGuid)
        });
        env.Sequence.Build();

        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, 0, 1));
        Assert.Equal(new[] { 1, 3 }, QueryIds(env.Sequence, 1, 100L));
        Assert.Equal(new[] { 2, 3 }, QueryIds(env.Sequence, 2, "BOB"));
        Assert.Equal(new[] { 1, 3 }, QueryIds(env.Sequence, 3, FirstGuid));
    }

    [Fact]
    public void Dynamic_Append_Shadows_Static_Record_And_Does_Not_Return_Stale_Key()
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
            Row(1, "ALICE", 100L, FirstGuid),
            Row(2, "BOB", 200L, SecondGuid)
        });
        env.Sequence.Build();
        env.Sequence.AppendElement(Row(1, "CAROL", 300L, FirstGuid));

        Assert.Empty(QueryIds(env.Sequence, 0, "alice"));
        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, 0, "carol"));
    }

    [Fact]
    public void Compact_Rebuilds_Static_Index_From_Current_Records()
    {
        using var env = new SequenceEnvironment();
        var nameIndex = new ExternalKeyIndex<string>(
            env.StreamGen,
            env.Sequence,
            r => new[] { (string)((object[])r)[1] },
            StringComparer.OrdinalIgnoreCase);
        env.Sequence.uindexes = new IUIndex[] { nameIndex };

        env.Sequence.Load(new object[]
        {
            Row(1, "ALICE", 100L, FirstGuid),
            Row(2, "BOB", 200L, SecondGuid)
        });
        env.Sequence.Build();
        env.Sequence.AppendElement(Row(1, "CAROL", 300L, FirstGuid));

        nameIndex.Compact();
        nameIndex.Refresh();

        Assert.Empty(QueryIds(env.Sequence, 0, "alice"));
        Assert.Equal(new[] { 1 }, QueryIds(env.Sequence, 0, "carol"));
        Assert.Equal(new[] { 2 }, QueryIds(env.Sequence, 0, "bob"));
    }

    private static object[] Row(int id, string name, long score, Guid guid)
    {
        return new object[] { id, name, score, guid.ToString("D") };
    }

    private static int[] QueryIds(USequence sequence, int index, IComparable key)
    {
        return sequence.GetAllByValue(index, key, _ => Array.Empty<IComparable>())
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
                    new NamedType("name", new PType(PTypeEnumeration.sstring)),
                    new NamedType("score", new PType(PTypeEnumeration.longinteger)),
                    new NamedType("guid", new PType(PTypeEnumeration.sstring))),
                null,
                StreamGen,
                _ => false,
                r => (int)((object[])r)[0],
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
