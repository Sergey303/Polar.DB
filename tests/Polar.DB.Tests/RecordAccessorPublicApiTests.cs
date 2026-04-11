using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Additional public-API tests for <see cref="RecordAccessor"/>.
///
/// The goal of this file is not to repeat the existing happy-path coverage,
/// but to lock down the public contract that external callers actually use:
/// schema metadata access, guard clauses, typed reads, and empty-shape creation.
/// </summary>
public class RecordAccessorPublicApiTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));

    /// <summary>
    /// Verifies that the constructor rejects a missing schema immediately,
    /// because all other API members depend on stable field metadata.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullRecordType()
    {
        Assert.Throws<ArgumentNullException>(() => new RecordAccessor(null!));
    }

    /// <summary>
    /// Verifies that the schema exposed by the accessor remains observable
    /// through the dedicated metadata properties.
    /// </summary>
    [Fact]
    public void RecordType_FieldCount_And_FieldNames_ExposeSchemaMetadata()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.Same(PersonType, accessor.RecordType);
        Assert.Equal(3, accessor.FieldCount);
        Assert.Equal(new[] { "id", "name", "age" }, accessor.FieldNames.ToArray());
    }

    /// <summary>
    /// Verifies positive and negative field existence checks.
    /// </summary>
    [Fact]
    public void HasField_ReflectsSchemaMembership()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.True(accessor.HasField("id"));
        Assert.True(accessor.HasField("name"));
        Assert.False(accessor.HasField("missing"));
    }

    /// <summary>
    /// Verifies that the field type lookup returns the exact schema type object.
    /// </summary>
    [Fact]
    public void GetFieldType_ReturnsDeclaredFieldType()
    {
        var accessor = new RecordAccessor(PersonType);

        var idType = accessor.GetFieldType("id");
        var nameType = accessor.GetFieldType("name");

        Assert.Equal(PTypeEnumeration.integer, idType.Vid);
        Assert.Equal(PTypeEnumeration.sstring, nameType.Vid);
    }

    /// <summary>
    /// Verifies that an empty record instance can be created with the exact
    /// expected array shape even when no values are supplied yet.
    /// </summary>
    [Fact]
    public void CreateRecord_WithoutValues_ReturnsEmptyRecordWithExpectedShape()
    {
        var accessor = new RecordAccessor(PersonType);

        var record = accessor.CreateRecord();

        Assert.Equal(3, record.Length);
        Assert.All(record, value => Assert.Null(value));
    }

    /// <summary>
    /// Verifies that shape validation rejects a missing record instance.
    /// </summary>
    [Fact]
    public void ValidateShape_ThrowsOnNullRecord()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.Throws<ArgumentNullException>(() => accessor.ValidateShape(null!));
    }

    /// <summary>
    /// Verifies that shape validation rejects values that are not represented
    /// as <see cref="object"/> arrays.
    /// </summary>
    [Fact]
    public void ValidateShape_ThrowsWhenRecordIsNotObjectArray()
    {
        var accessor = new RecordAccessor(PersonType);

        var ex = Assert.Throws<ArgumentException>(() => accessor.ValidateShape("not-an-array"));
        Assert.Contains("object[]", ex.Message);
    }

    /// <summary>
    /// Verifies the successful typed access path of <c>TryGet&lt;T&gt;</c>.
    /// </summary>
    [Fact]
    public void TryGet_Generic_ReturnsTypedValue_WhenFieldExistsAndTypeMatches()
    {
        var accessor = new RecordAccessor(PersonType);
        object record = new object[] { 7, "Ivanov", 20 };

        var ok = accessor.TryGet<int>(record, "age", out var age);

        Assert.True(ok);
        Assert.Equal(20, age);
    }

    /// <summary>
    /// Verifies that <c>TryGet&lt;T&gt;</c> is conservative and does not perform
    /// unchecked conversions when the runtime type does not match the requested type.
    /// </summary>
    [Fact]
    public void TryGet_Generic_ReturnsFalse_WhenRuntimeTypeDoesNotMatchRequestedType()
    {
        var accessor = new RecordAccessor(PersonType);
        object record = new object[] { 7, "Ivanov", 20 };

        var ok = accessor.TryGet<string>(record, "age", out var ageAsString);

        Assert.False(ok);
        Assert.Null(ageAsString);
    }

    /// <summary>
    /// Verifies a representative guard clause on field-name based access.
    /// </summary>
    [Fact]
    public void GetIndex_ThrowsOnNullFieldName()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.Throws<ArgumentNullException>(() => accessor.GetIndex(null!));
    }

    /// <summary>
    /// Verifies that schema metadata guards also reject null field names.
    /// </summary>
    [Fact]
    public void HasField_ThrowsOnNullFieldName()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.Throws<ArgumentNullException>(() => accessor.HasField(null!));
    }
}
