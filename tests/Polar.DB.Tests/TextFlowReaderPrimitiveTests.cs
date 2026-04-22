using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Direct tests for public instance reader methods of <see cref="TextFlow"/>.
///
/// <para>
/// These tests verify that the low-level reader surface can parse valid serialized text
/// for the primitive branches that are exposed as instance methods.
/// </para>
///
/// <para>
/// The goal is to lock in the reader-level contract itself, not only the higher-level
/// static <c>Serialize</c>/<c>Deserialize</c> round-trips.
/// </para>
/// </summary>
public class TextFlowReaderPrimitiveTests
{
    /// <summary>
    /// Verifies that <see cref="TextFlow.Skip"/> advances over leading whitespace
    /// and allows the next primitive read to start at the first meaningful token.
    ///
    /// <para>
    /// This protects the reader contract used by the public parser entry points,
    /// where whitespace tolerance is expected before actual values.
    /// </para>
    /// </summary>
    [Fact]
    public void Skip_Skips_Leading_Whitespace_Before_Next_Value()
    {
        var flow = new TextFlow(new StringReader("   123"));

        flow.Skip();
        int value = flow.ReadInt32();

        Assert.Equal(123, value);
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.ReadBoolean"/> can read a boolean value
    /// produced by the current <see cref="TextFlow"/> serializer.
    ///
    /// <para>
    /// This is a direct positive reader-level contract test. It does not assert any
    /// strict invalid-token rejection rules beyond the currently used valid format.
    /// </para>
    /// </summary>
    [Fact]
    public void ReadBoolean_Parses_Serialized_Boolean()
    {
        string text = SerializePrimitive(true, new PType(PTypeEnumeration.boolean));
        var flow = new TextFlow(new StringReader(text));

        bool value = flow.ReadBoolean();

        Assert.True(value);
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.ReadByte"/> can read a byte value produced
    /// by the current serializer.
    ///
    /// <para>
    /// This protects the direct primitive reader contract, not just higher-level
    /// round-trip behavior through static parsing helpers.
    /// </para>
    /// </summary>
    [Fact]
    public void ReadByte_Parses_Serialized_Byte()
    {
        string text = SerializePrimitive((byte)25, new PType(PTypeEnumeration.@byte));
        var flow = new TextFlow(new StringReader(text));

        byte value = flow.ReadByte();

        Assert.Equal((byte)25, value);
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.ReadChar"/> can read a character value
    /// produced by the current serializer.
    ///
    /// <para>
    /// This protects the direct char reader contract independently from record or
    /// sequence parsing scenarios.
    /// </para>
    /// </summary>
    [Fact]
    public void ReadChar_Parses_Serialized_Character()
    {
        string text = SerializePrimitive('Z', new PType(PTypeEnumeration.character));
        var flow = new TextFlow(new StringReader(text));

        char value = flow.ReadChar();

        Assert.Equal('Z', value);
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.ReadInt32"/> can read a 32-bit integer value
    /// produced by the current serializer.
    ///
    /// <para>
    /// This locks in the primitive integer reader contract directly at the instance
    /// reader level.
    /// </para>
    /// </summary>
    [Fact]
    public void ReadInt32_Parses_Serialized_Integer()
    {
        string text = SerializePrimitive(12345, new PType(PTypeEnumeration.integer));
        var flow = new TextFlow(new StringReader(text));

        int value = flow.ReadInt32();

        Assert.Equal(12345, value);
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.ReadInt64"/> can read a 64-bit integer value
    /// produced by the current serializer.
    ///
    /// <para>
    /// This protects the large-integer reader branch separately from the general
    /// deserialize round-trip tests.
    /// </para>
    /// </summary>
    [Fact]
    public void ReadInt64_Parses_Serialized_LongInteger()
    {
        string text = SerializePrimitive(1234567890123L, new PType(PTypeEnumeration.longinteger));
        var flow = new TextFlow(new StringReader(text));

        long value = flow.ReadInt64();

        Assert.Equal(1234567890123L, value);
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.ReadDouble"/> can read a real value produced
    /// by the current serializer.
    ///
    /// <para>
    /// This protects the floating-point reader contract directly at the primitive
    /// instance-reader level.
    /// </para>
    /// </summary>
    [Fact]
    public void ReadDouble_Parses_Serialized_Real()
    {
        string text = SerializePrimitive(12.5, new PType(PTypeEnumeration.real));
        var flow = new TextFlow(new StringReader(text));

        double value = flow.ReadDouble();

        Assert.Equal(12.5, value, 6);
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.ReadString"/> can read a serialized string
    /// including escape processing expected by the current text format.
    ///
    /// <para>
    /// This test complements higher-level string parsing tests by exercising the
    /// reader primitive directly.
    /// </para>
    /// </summary>
    [Fact]
    public void ReadString_Parses_Serialized_String()
    {
        string text = SerializePrimitive("A\"B\\C", new PType(PTypeEnumeration.sstring));
        var flow = new TextFlow(new StringReader(text));

        string value =flow.ReadString();

        Assert.Equal("A\"B\\C", value);
    }

    private static string SerializePrimitive(object value, PType type)
    {
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, value, type);
        return writer.ToString();
    }
}
