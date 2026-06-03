using Xunit;

namespace Polar.DB.Tests;

public class RecordAccessorTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));
    private static readonly RecordAccessor PersonAccessor = new(PersonType);

    [Fact]
    public void GetIndex_Returns_Stable_Field_Position()
    {
        var accessor = new RecordAccessor(PersonType);

        Assert.Equal(0, accessor.GetIndex("id"));
        Assert.Equal(1, accessor.GetIndex("name"));
        Assert.Equal(2, accessor.GetIndex("age"));
    }

    [Fact]
    public void Get_And_Set_By_Field_Name_Work()
    {
        var accessor = new RecordAccessor(PersonType);
        object record = new object[] { 7, "Ivanov", 20 };

        Assert.Equal(20, accessor.Get<int>(record, "age"));

        accessor.Set(record, "age", 21);

        Assert.Equal(21, accessor.Get<int>(record, "age"));
    }

    [Fact]
    public void CreateRecord_Creates_Array_With_Expected_Field_Count()
    {
        var accessor = new RecordAccessor(PersonType);

        var record = accessor.CreateRecord(1, "Petrov", 33);

        Assert.Equal(3, record.Length);
        Assert.Equal(1, record[0]);
        Assert.Equal("Petrov", record[1]);
        Assert.Equal(33, record[2]);
    }

    [Fact]
    public void ValidateShape_Throws_On_Invalid_Field_Count()
    {
        var accessor = new RecordAccessor(PersonType);
        object invalid = new object[] { 1, "OnlyTwoFields" };

        var ex = Assert.Throws<ArgumentException>(() => accessor.ValidateShape(invalid));
        Assert.Contains("Record field count mismatch", ex.Message);
    }

    [Fact]
    public void TryGet_Returns_False_For_Missing_Field()
    {
        var accessor = new RecordAccessor(PersonType);
        object record = new object[] { 7, "Ivanov", 20 };

        var ok = accessor.TryGet(record, "missing", out var value);

        Assert.False(ok);
        Assert.Null(value);
    }
    
    [Fact]
    public void CreateRecord_Get_And_Set_Work_For_Typical_Record()
    {
        var record = PersonAccessor.CreateRecord(7, "Ivanov", 20);

        Assert.Equal(7, PersonAccessor.Get<int>(record, "id"));
        Assert.Equal("Ivanov", PersonAccessor.Get<string>(record, "name"));
        Assert.Equal(20, PersonAccessor.Get<int>(record, "age"));

        PersonAccessor.Set(record, "age", 21);
        PersonAccessor.Set(record, "name", "Petrov");

        Assert.Equal(21, PersonAccessor.Get<int>(record, "age"));
        Assert.Equal("Petrov", PersonAccessor.Get<string>(record, "name"));
    }

    [Fact]
    public void FieldNames_Preserve_Schema_Order()
    {
        Assert.Equal(new[] { "id", "name", "age" }, PersonAccessor.FieldNames.ToArray());
    }

    [Fact]
    public void CreateRecord_Preserves_Field_Order_And_Value_Positions()
    {
        var record = PersonAccessor.CreateRecord(10, "Alice", 30);

        var row = Assert.IsType<object[]>(record);
        Assert.Equal(3, row.Length);
        Assert.Equal(10, (int)row[0]);
        Assert.Equal("Alice", (string)row[1]);
        Assert.Equal(30, (int)row[2]);
    }

    [Fact]
    public void Works_On_Existing_ObjectArray_Record_Without_Recreating_Record()
    {
        object record = new object[] { 11, "Bob", 35 };

        Assert.Equal(11, PersonAccessor.Get<int>(record, "id"));
        Assert.Equal("Bob", PersonAccessor.Get<string>(record, "name"));
        Assert.Equal(35, PersonAccessor.Get<int>(record, "age"));

        PersonAccessor.Set(record, "age", 36);

        Assert.Equal(36, PersonAccessor.Get<int>(record, "age"));
    }

    [Fact]
    public void Get_With_Unknown_Field_Name_Throws()
    {
        var record = PersonAccessor.CreateRecord(1, "Alice", 30);

        Assert.ThrowsAny<Exception>(() => PersonAccessor.Get<int>(record, "missing"));
    }

    [Fact]
    public void Set_With_Unknown_Field_Name_Throws()
    {
        var record = PersonAccessor.CreateRecord(1, "Alice", 30);

        Assert.ThrowsAny<Exception>(() => PersonAccessor.Set(record, "missing", 123));
    }

    [Fact]
    public void Duplicate_Field_Names_Are_Rejected()
    {
        var duplicateType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("id", new PType(PTypeEnumeration.sstring)));

        Assert.ThrowsAny<Exception>(() => new RecordAccessor(duplicateType));
    }

    [Fact]
    public void Wrong_Typed_Value_Does_Not_Silently_Behave_As_Correct_Strong_Type()
    {
        var record = PersonAccessor.CreateRecord(1, "Alice", 30);

        var setException = Record.Exception(() => PersonAccessor.Set(record, "age", "thirty"));
        if (setException != null)
        {
            Assert.NotNull(setException);
            return;
        }

        Assert.ThrowsAny<Exception>(() => PersonAccessor.Get<int>(record, "age"));
    }

    [Fact]
    public void Empty_Record_Type_Can_Be_Accessed()
    {
        var emptyType = new PTypeRecord();
        var emptyAccessor = new RecordAccessor(emptyType);
        var record = emptyAccessor.CreateRecord();

        Assert.Empty(emptyAccessor.FieldNames);
        Assert.Empty(Assert.IsType<object[]>(record));
    }

    [Fact]
    public void Minimal_Single_Field_Record_Works()
    {
        var oneFieldType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)));
        var oneFieldAccessor = new RecordAccessor(oneFieldType);

        var record = oneFieldAccessor.CreateRecord(42);

        Assert.Equal(new[] { "id" }, oneFieldAccessor.FieldNames.ToArray());
        Assert.Equal(42, oneFieldAccessor.Get<int>(record, "id"));

        oneFieldAccessor.Set(record, "id", 43);
        Assert.Equal(43, oneFieldAccessor.Get<int>(record, "id"));
    }

    [Fact]
    public void HasField_Returns_True_For_Existing_Field_And_False_For_Missing_Field()
    {
        Assert.True(PersonAccessor.HasField("id"));
        Assert.False(PersonAccessor.HasField("missing"));
    }

    [Fact]
    public void HasField_Throws_ArgumentNullException_For_Null()
    {
        Assert.Throws<ArgumentNullException>(() => PersonAccessor.HasField(null!));
    }

    [Fact]
    public void GetFieldType_Returns_Declared_Field_Type()
    {
        var idType = PersonAccessor.GetFieldType("id");
        var nameType = PersonAccessor.GetFieldType("name");

        Assert.Equal(PTypeEnumeration.integer, idType.Vid);
        Assert.Equal(PTypeEnumeration.sstring, nameType.Vid);
    }

    [Fact]
    public void GetIndex_Throws_ArgumentNullException_For_Null()
    {
        Assert.Throws<ArgumentNullException>(() => PersonAccessor.GetIndex(null!));
    }

    [Fact]
    public void TryGet_Typed_Returns_True_For_Correct_Type()
    {
        object record = new object[] { 7, "Ivanov", 20 };

        var ok = PersonAccessor.TryGet<int>(record, "age", out var age);

        Assert.True(ok);
        Assert.Equal(20, age);
    }

    [Fact]
    public void TryGet_Typed_Returns_False_For_Wrong_Type()
    {
        object record = new object[] { 7, "Ivanov", 20 };

        var ok = PersonAccessor.TryGet<string>(record, "age", out var wrongTypeValue);

        Assert.False(ok);
        Assert.Null(wrongTypeValue);
    }

    [Fact]
    public void ValidateShape_Throws_On_NonObjectArray()
    {
        var ex = Assert.Throws<ArgumentException>(() => PersonAccessor.ValidateShape("not-an-array"));
        Assert.Contains("Record value must be object[]", ex.Message);
    }

    [Fact]
    public void CreateRecord_Params_Throws_On_Null()
    {
        Assert.Throws<ArgumentNullException>(() => PersonAccessor.CreateRecord(null!));
    }

    [Fact]
    public void CreateRecord_Params_Throws_On_Wrong_Field_Count()
    {
        var ex = Assert.Throws<ArgumentException>(() => PersonAccessor.CreateRecord(1, "OnlyTwo"));
        Assert.Contains("Record field count mismatch", ex.Message);
    }
}
