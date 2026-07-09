using System.Text;
using Xunit;

namespace Polar.DB.Tests;

public class SerializationContractTests
{
    [Fact]
    public void ByteFlow_FixedString_RoundTrip_UsesDeclaredHeadSize()
    {
        var type = new PTypeFString(5);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            ByteFlow.Serialize(writer, "AB", type);
            writer.Flush();
        }

        Assert.Equal(type.HeadSize, stream.Length);
        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("AB", Assert.IsType<string>(ByteFlow.Deserialize(reader, type)));
    }

    [Fact]
    public void ByteFlow_FixedString_RejectsValueLongerThanDeclaredLength()
    {
        var type = new PTypeFString(3);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        Assert.Throws<ArgumentException>(() => ByteFlow.Serialize(writer, "TOO LONG", type));
    }

    [Fact]
    public void TextFlow_FixedString_RoundTrip_PreservesUnicode()
    {
        var type = new PTypeFString(4);
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, "Ёж", type);

        string text = writer.ToString();
        Assert.Equal("\"Ёж\"", text);
        Assert.Equal("Ёж", Assert.IsType<string>(TextFlow.Deserialize(new StringReader(text), type)));
    }

    [Fact]
    public void TextFlow_FixedString_RejectsOversizedInput()
    {
        var type = new PTypeFString(2);
        Assert.Throws<InvalidDataException>(
            () => TextFlow.Deserialize(new StringReader("\"ABC\""), type));
    }

    [Fact]
    public void TextFlow_UnterminatedString_ThrowsEndOfStreamException()
    {
        var type = new PType(PTypeEnumeration.sstring);
        Assert.Throws<EndOfStreamException>(
            () => TextFlow.Deserialize(new StringReader("\"missing end"), type));
    }

    [Fact]
    public void TextFlow_InvalidBoolean_ThrowsInvalidDataException()
    {
        var type = new PType(PTypeEnumeration.boolean);
        Assert.Throws<InvalidDataException>(() => TextFlow.Deserialize(new StringReader("x"), type));
    }

    [Fact]
    public void TextFlow_Byte_UsesDecimalSyntax()
    {
        var type = new PType(PTypeEnumeration.@byte);
        Assert.Equal((byte)255, Assert.IsType<byte>(TextFlow.Deserialize(new StringReader("255"), type)));
        Assert.Throws<InvalidDataException>(() => TextFlow.Deserialize(new StringReader("af"), type));
    }
}
