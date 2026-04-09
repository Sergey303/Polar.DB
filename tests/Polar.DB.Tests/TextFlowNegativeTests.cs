using Xunit;

namespace Polar.DB.Tests;

public class TextFlowNegativeTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    private static readonly PTypeUnion SampleUnionType = new(
        new NamedType("i", new PType(PTypeEnumeration.integer)),
        new NamedType("s", new PType(PTypeEnumeration.sstring)));

    [Fact]
    public void Deserialize_String_With_Unfinished_Escape_Throws()
    {
        using var reader = new StringReader("\"abc\\");

        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, new PType(PTypeEnumeration.sstring)));
    }

    [Fact]
    public void Deserialize_Record_With_Truncated_Input_Throws()
    {
        using var reader = new StringReader("{1,\"A\"");

        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, PersonType));
    }

    [Fact]
    public void Deserialize_Sequence_With_Invalid_Syntax_Throws()
    {
        using var reader = new StringReader("[1,2");

        Assert.ThrowsAny<Exception>(() => TextFlow.DeserializeSequenseToFlow(reader, new PType(PTypeEnumeration.integer)).ToArray());
    }

    [Fact]
    public void Deserialize_Union_With_Invalid_Tag_Throws_Or_Fails()
    {
        using var reader = new StringReader("9^1");

        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, SampleUnionType));
    }
}
