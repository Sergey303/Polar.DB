using System.Text;
using Xunit;

namespace Polar.DB.Tests;

public class GuidTypeTests
{
    private static readonly PType GuidType = new(PTypeEnumeration.guid);

    [Fact]
    public void PType_Guid_IsFixedAtom_And_RoundTripsTypeDescription()
    {
        Assert.True(GuidType.IsAtom);
        Assert.True(GuidType.HasNoTail);
        Assert.Equal(16, GuidType.HeadSize);

        object description = GuidType.ToPObject(3);
        PType restored = PType.FromPObject(description);

        Assert.Equal(PTypeEnumeration.guid, restored.Vid);
        Assert.Equal("guid^", PType.TType.Interpret(description));
    }

    [Fact]
    public void ByteFlow_Guid_RoundTrips_AsExactly16Bytes_AndSkips()
    {
        var first = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var second = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        ByteFlow.Serialize(writer, first, GuidType);
        ByteFlow.Serialize(writer, second, GuidType);
        writer.Flush();

        Assert.Equal(32, stream.Length);

        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        ByteFlow.Skip(reader, GuidType);
        Assert.Equal(16, stream.Position);
        Assert.Equal(second, Assert.IsType<Guid>(ByteFlow.Deserialize(reader, GuidType)));
    }

    [Fact]
    public void TextFlow_Guid_UsesCanonicalQuotedForm_AndRoundTrips()
    {
        var value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        using var writer = new StringWriter();

        TextFlow.Serialize(writer, value, GuidType);
        string text = writer.ToString();

        Assert.Equal("\"00112233-4455-6677-8899-aabbccddeeff\"", text);
        Assert.Equal(value, Assert.IsType<Guid>(
            TextFlow.Deserialize(new StringReader(text), GuidType)));
    }

    [Fact]
    public void UniversalSequenceBase_Guid_UsesFixed16ByteOffsets_AndRefreshes()
    {
        var first = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var second = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
        using var stream = new MemoryStream();
        using var sequence = new UniversalSequenceBase(GuidType, stream);

        Assert.Equal(8L, sequence.AppendElement(first));
        Assert.Equal(24L, sequence.AppendElement(second));
        sequence.Flush();

        Assert.Equal(40L, sequence.AppendOffset);
        Assert.Equal(24L, sequence.ElementOffset(1));
        Assert.Equal(second, Assert.IsType<Guid>(sequence.GetByIndex(1)));

        sequence.Refresh();

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(first, Assert.IsType<Guid>(sequence.GetByIndex(0)));
        Assert.Equal(second, Assert.IsType<Guid>(sequence.GetByIndex(1)));
    }

    [Fact]
    public void Record_WithGuid_RemainsFixedSize()
    {
        var record = new PTypeRecord(
            new NamedType("id", GuidType),
            new NamedType("version", new PType(PTypeEnumeration.integer)));

        Assert.True(record.HasNoTail);
        Assert.Equal(20, record.HeadSize);
    }
}

