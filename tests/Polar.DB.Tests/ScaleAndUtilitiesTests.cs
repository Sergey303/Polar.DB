using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class ScaleAndUtilitiesTests
{
    [Fact]
    public void Diapason_Empty_IsMarkedAsEmpty()
    {
        var empty = Diapason.Empty;

        Assert.True(empty.IsEmpty());
        Assert.Equal(long.MinValue, empty.start);
        Assert.Equal(0L, empty.numb);
    }

    [Fact]
    public void Diapason_WithPositiveLength_IsNotEmpty()
    {
        var diapason = new Diapason { start = 10, numb = 3 };

        Assert.False(diapason.IsEmpty());
        Assert.Equal(10L, diapason.start);
        Assert.Equal(3L, diapason.numb);
    }

    [Fact]
    public void HashRot13_IsStable_ForSameInput()
    {
        var first = Hashfunctions.HashRot13("Привет");
        var second = Hashfunctions.HashRot13("Привет");

        Assert.Equal(first, second);
    }

    [Fact]
    public void First4charsRu_IsCaseInsensitive_And_PadsShortStrings()
    {
        Assert.Equal(Hashfunctions.First4charsRu("ab"), Hashfunctions.First4charsRu("AB  "));
        Assert.Equal(Hashfunctions.First4charsRu("ёж"), Hashfunctions.First4charsRu("ЁЖ  "));
        Assert.NotEqual(Hashfunctions.First4charsRu("abcd"), Hashfunctions.First4charsRu("abce"));
    }

    [Fact]
    public void ObjOff_Constructor_Assigns_Object_And_Offset()
    {
        var value = new object[] { 1, "A" };
        var pair = new ObjOff(value, 42L);

        Assert.Same(value, pair.obj);
        Assert.Equal(42L, pair.off);
    }
}
