using Xunit;

namespace Polar.DB.Tests;

public class TextFlowReaderNegativeTests
{
    [Fact]
    public void ReadByte_With_Invalid_Token_Throws()
    {
        var flow = new TextFlow( new StringReader("999"));
        Assert.ThrowsAny<Exception>(() => flow.ReadByte());
    }

    [Fact]
    public void ReadInt32_With_Invalid_Token_Throws()
    {
        var flow = new TextFlow( new StringReader("abc"));
        Assert.ThrowsAny<Exception>(() => flow.ReadInt32());
    }

    [Fact]
    public void ReadInt64_With_Invalid_Token_Throws()
    {
        var flow = new TextFlow( new StringReader("abc"));
        Assert.ThrowsAny<Exception>(() => flow.ReadInt64());
    }

    [Fact]
    public void ReadDouble_With_Invalid_Token_Throws()
    {
        var flow = new TextFlow( new StringReader("abc"));
        Assert.ThrowsAny<Exception>(() => flow.ReadDouble());
    }

    [Fact]
    public void ReadString_With_Unterminated_Quoted_Text_Throws()
    {
        var flow = new TextFlow( new StringReader("\"abc"));
        Assert.ThrowsAny<Exception>(() => flow.ReadString());
    }

    [Fact]
    public void ReadString_With_Unfinished_Escape_Throws()
    {
        var flow = new TextFlow( new StringReader("\"abc\\"));
        Assert.ThrowsAny<Exception>(() => flow.ReadString());
    }
}
