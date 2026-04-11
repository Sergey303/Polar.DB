using System.Text;
using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Additional serialization and type-round-trip tests.
///
/// This file intentionally mixes two adjacent concerns:
/// 1. nested and edge-case <see cref="ByteFlow"/> round-trips;
/// 2. regression tests for <c>PType -&gt; object -&gt; PType</c> semantics.
///
/// The PType conversion helper is reflection-based on purpose, because the exact
/// conversion entry-point may differ between repository revisions while the semantic
/// contract should stay the same.
/// </summary>
public class ByteFlowEdgeTests
{
    /// <summary>
    /// Verifies a realistic nested value shape: a sequence of records.
    /// </summary>
    [Fact]
    public void Serialize_And_Deserialize_SequenceOfRecord_RoundTrip()
    {
        var personType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        var type = new PTypeSequence(personType);
        object[] value =
        {
            new object[] { 1, "ALICE" },
            new object[] { 2, "BOB" }
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        ByteFlow.Serialize(writer, value, type);
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var restored = Assert.IsType<object[]>(ByteFlow.Deserialize(reader, type));

        Assert.Equal(2, restored.Length);

        var first = Assert.IsType<object[]>(restored[0]);
        var second = Assert.IsType<object[]>(restored[1]);

        Assert.Equal(1, Assert.IsType<int>(first[0]));
        Assert.Equal("ALICE", Assert.IsType<string>(first[1]));
        Assert.Equal(2, Assert.IsType<int>(second[0]));
        Assert.Equal("BOB", Assert.IsType<string>(second[1]));
    }

    /// <summary>
    /// Verifies a nested record that combines a union field and a sequence field.
    /// </summary>
    [Fact]
    public void Serialize_And_Deserialize_NestedRecordWithUnionAndSequence_RoundTrip()
    {
        var payloadType = new PTypeUnion(
            new NamedType("i", new PType(PTypeEnumeration.integer)),
            new NamedType("s", new PType(PTypeEnumeration.sstring)));

        var envelopeType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("payload", payloadType),
            new NamedType("tags", new PTypeSequence(new PType(PTypeEnumeration.sstring))));

        object[] value =
        {
            7,
            new object[] { 1, "hello" },
            new object[] { "x", "y" }
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        ByteFlow.Serialize(writer, value, envelopeType);
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var restored = Assert.IsType<object[]>(ByteFlow.Deserialize(reader, envelopeType));

        Assert.Equal(7, Assert.IsType<int>(restored[0]));

        var payload = Assert.IsType<object[]>(restored[1]);
        Assert.Equal(1, Assert.IsType<int>(payload[0]));
        Assert.Equal("hello", Assert.IsType<string>(payload[1]));

        var tags = Assert.IsType<object[]>(restored[2]);
        Assert.Equal(new[] { "x", "y" }, tags.Cast<string>().ToArray());
    }

    /// <summary>
    /// Verifies that deserialization does not silently succeed when the binary
    /// payload is truncated in the middle of a nested structure.
    /// </summary>
    [Fact]
    public void Deserialize_TruncatedSequenceOfRecord_Throws()
    {
        var personType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        var type = new PTypeSequence(personType);
        object[] value =
        {
            new object[] { 1, "ALICE" },
            new object[] { 2, "BOB" }
        };

        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            ByteFlow.Serialize(writer, value, type);
            writer.Flush();
            bytes = stream.ToArray();
        }

        var truncated = bytes.Take(bytes.Length - 1).ToArray();
        using var brokenStream = new MemoryStream(truncated);
        using var reader = new BinaryReader(brokenStream, Encoding.UTF8, true);

        Assert.ThrowsAny<Exception>(() => ByteFlow.Deserialize(reader, type));
    }


}
