using System.Text;
using Xunit;

namespace Polar.DB.Tests;

public class ByteFlowTests
{
    [Fact]
    public void Serialize_And_Deserialize_Integer_RoundTrip()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        ByteFlow.Serialize(writer, 12345, new PType(PTypeEnumeration.integer));
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        object restored = ByteFlow.Deserialize(reader, new PType(PTypeEnumeration.integer));

        Assert.Equal(12345, Assert.IsType<int>(restored));
    }

    [Fact]
    public void Serialize_And_Deserialize_Record_RoundTrip()
    {
        var type = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        object[] value = { 7, "Ivanov" };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        ByteFlow.Serialize(writer, value, type);
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        object[] restored = Assert.IsType<object[]>(ByteFlow.Deserialize(reader, type));

        Assert.Equal(7, Assert.IsType<int>(restored[0]));
        Assert.Equal("Ivanov", Assert.IsType<string>(restored[1]));
    }

    [Fact]
    public void Serialize_And_Deserialize_Sequence_RoundTrip()
    {
        var type = new PTypeSequence(new PType(PTypeEnumeration.integer));
        object[] value = { 1, 2, 3 };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        ByteFlow.Serialize(writer, value, type);
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        object[] restored = Assert.IsType<object[]>(ByteFlow.Deserialize(reader, type));

        Assert.Equal(new[] { 1, 2, 3 }, restored.Cast<int>().ToArray());
    }

    [Fact]
    public void Serialize_And_Deserialize_Union_RoundTrip()
    {
        var type = new PTypeUnion(
            new NamedType("i", new PType(PTypeEnumeration.integer)),
            new NamedType("s", new PType(PTypeEnumeration.sstring)));

        object[] value = { 1, "abc" };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        ByteFlow.Serialize(writer, value, type);
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        object[] restored = Assert.IsType<object[]>(ByteFlow.Deserialize(reader, type));

        Assert.Equal(1, Assert.IsType<int>(restored[0]));
        Assert.Equal("abc", Assert.IsType<string>(restored[1]));
    }

    [Fact]
    public void Serialize_And_Deserialize_Nested_Record_Sequence_Union_RoundTrip()
    {
        var pointType = new PTypeRecord(
            new NamedType("x", new PType(PTypeEnumeration.real)),
            new NamedType("y", new PType(PTypeEnumeration.real)));
        var shapeType = new PTypeUnion(
            new NamedType("point", pointType),
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("weight", new PType(PTypeEnumeration.integer)));
        var sceneType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("shapes", new PTypeSequence(shapeType)));

        object[] value =
        {
            101,
            new object[]
            {
                new object[] { 0, new object[] { 1.5, 2.5 } },
                new object[] { 1, "origin" },
                new object[] { 2, 9 }
            }
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        ByteFlow.Serialize(writer, value, sceneType);
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        object[] restored = Assert.IsType<object[]>(ByteFlow.Deserialize(reader, sceneType));

        Assert.Equal(101, Assert.IsType<int>(restored[0]));
        var shapes = Assert.IsType<object[]>(restored[1]);
        Assert.Equal(3, shapes.Length);

        var first = Assert.IsType<object[]>(shapes[0]);
        Assert.Equal(0, Assert.IsType<int>(first[0]));
        var firstPoint = Assert.IsType<object[]>(first[1]);
        Assert.Equal(1.5, Assert.IsType<double>(firstPoint[0]));
        Assert.Equal(2.5, Assert.IsType<double>(firstPoint[1]));

        var second = Assert.IsType<object[]>(shapes[1]);
        Assert.Equal(1, Assert.IsType<int>(second[0]));
        Assert.Equal("origin", Assert.IsType<string>(second[1]));

        var third = Assert.IsType<object[]>(shapes[2]);
        Assert.Equal(2, Assert.IsType<int>(third[0]));
        Assert.Equal(9, Assert.IsType<int>(third[1]));
    }

    [Fact]
    public void Serialize_Record_WithWrongFieldCount_Throws()
    {
        var type = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        var ex = Assert.Throws<Exception>(() => ByteFlow.Serialize(writer, new object[] { 1 }, type));
        Assert.Contains("wrong record field number", ex.Message);
    }

    [Fact]
    public void Serialize_Union_WithInvalidTag_Throws()
    {
        var type = new PTypeUnion(
            new NamedType("i", new PType(PTypeEnumeration.integer)));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        var ex = Assert.Throws<Exception>(() => ByteFlow.Serialize(writer, new object[] { 5, 123 }, type));
        Assert.Contains("wrong union tag", ex.Message);
    }

    [Fact]
    public void Deserialize_Sequence_WithNegativeLength_Throws()
    {
        var type = new PTypeSequence(new PType(PTypeEnumeration.integer));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(-1L);
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var ex = Assert.Throws<Exception>(() => ByteFlow.Deserialize(reader, type));
        Assert.Contains("too many", ex.Message);
    }
}