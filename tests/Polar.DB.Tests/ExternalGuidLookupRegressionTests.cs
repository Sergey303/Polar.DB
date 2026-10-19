using Polar.DB.ExternalKey;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class ExternalGuidLookupRegressionTests
{
    private static readonly Guid Alpha = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Beta = Guid.Parse("99999999-8888-7777-6666-555555555555");

    [Fact]
    public void Repeated_Built_Guid_Lookups_Deduplicate_Filter_And_Shadow_Updates()
    {
        var streams = new List<Stream>();
        Stream OpenStream()
        {
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        }

        var type = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("guid", new PType(PTypeEnumeration.guid)),
            new NamedType("text", new PType(PTypeEnumeration.sstring)),
            new NamedType("deleted", new PType(PTypeEnumeration.boolean)));

        using var sequence = new USequence(type, null, OpenStream,
            row => (bool)((object[])row)[3], optimise: false);
        sequence.SetPrimaryKey<int>(row => (int)((object[])row)[0]);

        // USequence owns the index and disposes it after the final Flush.
        var index = new ExternalKeyIndex<Guid>(OpenStream, sequence,
            row => new[] { (Guid)((object[])row)[1], (Guid)((object[])row)[1] });
        sequence.uindexes = new IUIndex[] { index };

        sequence.Load(new object[]
        {
            Row(1, Alpha, "keep"),
            Row(2, Beta, "other"),
            Row(3, Alpha, "skip")
        });
        sequence.Build();

        for (int iteration = 0; iteration < 4; iteration++)
        {
            Assert.Equal(new[] { 1, 3 }, Ids(index.GetManyByValue(Alpha)));
            Assert.Equal(new[] { 1 }, Ids(index.GetManyByValue(Alpha,
                row => (string)((object[])row)[2] == "keep")));
            Assert.Empty(index.GetManyByValue(Guid.Empty));
        }

        sequence.AppendElement(Row(3, Beta, "replacement"));
        Assert.Equal(new[] { 1 }, Ids(index.GetManyByValue(Alpha)));
        Assert.Equal(new[] { 2, 3 }, Ids(index.GetManyByValue(Beta)));

        sequence.AppendElement(Row(1, Alpha, "removed", deleted: true));
        Assert.Empty(index.GetManyByValue(Alpha));

        sequence.Build();
        index.Refresh();
        Assert.Empty(index.GetManyByValue(Alpha));
        Assert.Equal(new[] { 2, 3 }, Ids(index.GetManyByValue(Beta)));

    }

    private static object[] Row(int id, Guid guid, string text, bool deleted = false) =>
        new object[] { id, guid, text, deleted };

    private static int[] Ids(IEnumerable<object> values) =>
        values.Cast<object[]>().Select(row => (int)row[0]).OrderBy(id => id).ToArray();
}
