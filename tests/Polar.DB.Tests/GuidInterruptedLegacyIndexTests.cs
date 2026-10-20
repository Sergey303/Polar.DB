using Polar.DB.ExternalKey;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class GuidInterruptedLegacyIndexTests
{
    private static readonly Guid Tag = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Interrupted_Legacy_String_Index_Rebuilds_Natively_From_Preserved_Rows()
    {
        var streams = new List<Stream>();
        Stream DataStream()
        {
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        }

        using var sequence = new USequence(
            new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("tag", new PType(PTypeEnumeration.guid))),
            null, DataStream, _ => false, optimise: false);
        sequence.SetPrimaryKey<int>(row => (int)((object[])row)[0]);
        sequence.AppendElement(new object[] { 1, Tag });
        sequence.AppendElement(new object[] { 2, Tag });
        sequence.Build();

        using var keyStream = new MemoryStream();
        using (var writer = new BinaryWriter(keyStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(2L);
            writer.Write(Tag.ToString("N"));
            writer.Write(Tag.ToString("N"));
            writer.Write((byte)0xAB); // Crash during another legacy key.
            writer.Flush();
        }
        keyStream.Position = 0;
        using var offsetStream = new MemoryStream();

        int next = 0;
        Stream OpenIndexStream() => next++ == 0 ? keyStream : offsetStream;
        using var index = new ExternalKeyIndex<Guid>(
            OpenIndexStream, sequence, row => new[] { (Guid)((object[])row)[1] });
        index.Refresh();

        Assert.Equal(new[] { 1, 2 }, index.GetManyByValue(Tag)
            .Cast<object[]>().Select(row => (int)row[0]).OrderBy(id => id).ToArray());
        Assert.Equal(40, keyStream.Length);
        Assert.Equal(24, offsetStream.Length);
    }
}
