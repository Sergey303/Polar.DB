using Xunit;

namespace Polar.DB.Tests;

public class TextFlowNegativeTests
{
    [Fact]
    public void Deserialize_String_With_Unfinished_Escape_Throws()
    {
        using var reader = new StringReader("\"line1\\");
        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, new PType(PTypeEnumeration.sstring)));
    }

    [Fact]
    public void Deserialize_Record_With_Truncated_Input_Throws()
    {
        var type = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        using var writer = new StringWriter();
        TextFlow.Serialize(writer, new object[] { 5, "Petrov" }, type);

        string malformed = RemoveLastChar(writer.ToString());

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, type));
    }

    [Fact]
    public void Deserialize_Sequence_With_Truncated_Input_Throws()
    {
        using var writer = new StringWriter();
        TextFlow.SerializeFlowToSequense(writer, new object[] { 10, 20, 30 }, new PType(PTypeEnumeration.integer));

        string malformed = RemoveLastChar(writer.ToString());

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() =>
            TextFlow.DeserializeSequenseToFlow(reader, new PType(PTypeEnumeration.integer)).ToArray());
    }

    [Fact]
    public void Deserialize_Sequence_With_Invalid_Element_Text_Throws()
    {
        using var writer = new StringWriter();
        TextFlow.SerializeFlowToSequense(writer, new object[] { 10, 20, 30 }, new PType(PTypeEnumeration.integer));

        string text = writer.ToString();
        string malformed = text.Replace("20", "XX");

        Assert.NotEqual(text, malformed);

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() =>
            TextFlow.DeserializeSequenseToFlow(reader, new PType(PTypeEnumeration.integer)).ToArray());
    }

    [Fact]
    public void Deserialize_Union_With_Truncated_Input_Throws()
    {
        var type = new PTypeUnion(
            new NamedType("i", new PType(PTypeEnumeration.integer)),
            new NamedType("s", new PType(PTypeEnumeration.sstring)));

        using var writer = new StringWriter();
        TextFlow.Serialize(writer, new object[] { 1, "abc" }, type);

        string malformed = RemoveLastChar(writer.ToString());

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, type));
    }

    private static string RemoveLastChar(string text)
    {
        Assert.False(string.IsNullOrEmpty(text));
        return text.Substring(0, text.Length - 1);
    }
}
