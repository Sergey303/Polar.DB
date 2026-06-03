using Xunit;

namespace Polar.DB.Tests;

public class TextFlowPrimitiveTests
{
    [Fact]
    public void Serialize_And_Deserialize_Boolean_Textual_RoundTrip()
    {
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, true, new PType(PTypeEnumeration.boolean));

        using var reader = new StringReader(writer.ToString());
        var restored = TextFlow.Deserialize(reader, new PType(PTypeEnumeration.boolean));

        Assert.True(Assert.IsType<bool>(restored));
    }

    [Fact]
    public void Serialize_And_Deserialize_Character_Textual_RoundTrip()
    {
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, 'Q', new PType(PTypeEnumeration.character));

        using var reader = new StringReader(writer.ToString());
        var restored = TextFlow.Deserialize(reader, new PType(PTypeEnumeration.character));

        Assert.Equal('Q', Assert.IsType<char>(restored));
    }

    [Fact]
    public void Serialize_And_Deserialize_LongInteger_Textual_RoundTrip()
    {
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, 9876543210123L, new PType(PTypeEnumeration.longinteger));

        using var reader = new StringReader(writer.ToString());
        var restored = TextFlow.Deserialize(reader, new PType(PTypeEnumeration.longinteger));

        Assert.Equal(9876543210123L, Assert.IsType<long>(restored));
    }

    [Fact]
    public void Serialize_And_Deserialize_Real_Textual_RoundTrip()
    {
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, 1234.5678, new PType(PTypeEnumeration.real));

        using var reader = new StringReader(writer.ToString());
        var restored = TextFlow.Deserialize(reader, new PType(PTypeEnumeration.real));

        Assert.Equal(1234.5678, Assert.IsType<double>(restored), 10);
    }

    [Fact]
    public void SerializeFlowToSequenseFormatted_Produces_Readable_Multiline_Output()
    {
        using var writer = new StringWriter();
        TextFlow.SerializeFlowToSequenseFormatted(writer, new object[] { 10, 20, 30 }, new PType(PTypeEnumeration.integer), 0);

        var text = writer.ToString();
        Assert.Contains('\n', text);
        Assert.Contains('[', text);
        Assert.Contains(']', text);
        Assert.Contains("10", text);
        Assert.Contains("20", text);
        Assert.Contains("30", text);
    }
}
