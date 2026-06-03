using Xunit;

namespace Polar.DB.Tests;

public class RecordAccessorPropertyTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));

    [Fact]
    public void RecordType_Returns_Original_Schema_Instance()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.Same(PersonType, accessor.RecordType);
    }

    [Fact]
    public void FieldCount_Returns_Number_Of_Declared_Fields()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.Equal(PersonType.Fields.Length, accessor.FieldCount);
    }
}
