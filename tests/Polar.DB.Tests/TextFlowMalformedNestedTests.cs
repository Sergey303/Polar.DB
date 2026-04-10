using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Regression tests for malformed nested <see cref="TextFlow"/> payloads.
///
/// <para>
/// These tests verify that the public parser entry points fail predictably when the
/// outer structure is syntactically plausible, but one of the nested payloads is broken.
/// </para>
///
/// <para>
/// The goal is not to prove every possible malformed-text case. The goal is to lock in
/// the useful contract: nested corruption must not be silently accepted as valid data.
/// </para>
/// </summary>
public class TextFlowMalformedNestedTests
{
    /// <summary>
    /// Verifies that <see cref="TextFlow.Deserialize(System.IO.TextReader, PType)"/> throws
    /// when an outer record contains a nested record whose closing delimiter is missing.
    ///
    /// <para>
    /// This protects against a regression where the parser could partially consume the nested
    /// object and still treat the outer record as valid.
    /// </para>
    /// </summary>
    [Fact]
    public void Deserialize_Record_With_Truncated_Nested_Record_Throws()
    {
        var nestedType = new PTypeRecord(
            new NamedType("code", new PType(PTypeEnumeration.integer)),
            new NamedType("text", new PType(PTypeEnumeration.sstring)));

        var outerType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("inner", nestedType));

        string valid = Serialize(new object[]
        {
            1,
            new object[] { 10, "alpha" }
        }, outerType);

        string malformed = RemovePenultimate(valid, '}');

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, outerType));
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.Deserialize(System.IO.TextReader, PType)"/> throws
    /// when an outer record contains a nested sequence whose closing bracket is missing.
    ///
    /// <para>
    /// This protects the parser from accepting truncated collection payloads embedded inside
    /// otherwise valid parent structures.
    /// </para>
    /// </summary>
    [Fact]
    public void Deserialize_Record_With_Truncated_Nested_Sequence_Throws()
    {
        var nestedType = new PTypeSequence(new PType(PTypeEnumeration.integer));
        var outerType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("items", nestedType));

        string valid = Serialize(new object[]
        {
            1,
            new object[] { 10, 20, 30 }
        }, outerType);

        string malformed = RemoveLast(valid, ']');

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, outerType));
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.Deserialize(System.IO.TextReader, PType)"/> throws
    /// when a nested union inside an outer record uses an invalid tag.
    ///
    /// <para>
    /// This protects the union branch-selection logic from silently mapping unknown tags to
    /// an unintended branch or continuing with corrupted nested state.
    /// </para>
    /// </summary>
    [Fact]
    public void Deserialize_Record_With_Invalid_Nested_Union_Tag_Throws()
    {
        var nestedType = new PTypeUnion(
            new NamedType("i", new PType(PTypeEnumeration.integer)),
            new NamedType("s", new PType(PTypeEnumeration.sstring)));

        var outerType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("value", nestedType));

        string valid = Serialize(new object[]
        {
            1,
            new object[] { 1, "abc" }
        }, outerType);

        int hatIndex = valid.IndexOf('^');
        Assert.True(hatIndex > 0, "Expected union tag marker '^' in serialized text.");

        string malformed = valid.Substring(0, hatIndex - 1) + "9" + valid.Substring(hatIndex);

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() => TextFlow.Deserialize(reader, outerType));
    }

    /// <summary>
    /// Verifies that <see cref="TextFlow.DeserializeSequenseToFlow(System.IO.TextReader, PType)"/>
    /// throws when a sequence element is itself a record and that nested record text is broken.
    ///
    /// <para>
    /// This protects the sequence parser from silently yielding a partially-read element when
    /// corruption occurs inside one nested item rather than at the outer sequence level.
    /// </para>
    /// </summary>
    [Fact]
    public void Deserialize_Sequence_With_Broken_Nested_Record_Element_Throws()
    {
        var elementType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        string valid = SerializeSequence(
            new object[]
            {
                new object[] { 1, "Alice" },
                new object[] { 2, "Bob" }
            },
            elementType);

        string malformed = RemoveFirst(valid, '}');

        using var reader = new StringReader(malformed);
        Assert.ThrowsAny<Exception>(() => TextFlow.DeserializeSequenseToFlow(reader, elementType).ToArray());
    }

    private static string Serialize(object value, PType type)
    {
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, value, type);
        return writer.ToString();
    }

    private static string SerializeSequence(IEnumerable<object> values, PType elementType)
    {
        using var writer = new StringWriter();
        TextFlow.SerializeFlowToSequense(writer, values, elementType);
        return writer.ToString();
    }

    private static string RemoveFirst(string text, char ch)
    {
        int index = text.IndexOf(ch);
        Assert.True(index >= 0, $"Expected '{ch}' in serialized text.");
        return text.Remove(index, 1);
    }

    private static string RemoveLast(string text, char ch)
    {
        int last = text.LastIndexOf(ch);
        Assert.True(last >= 0, $"Expected '{ch}' in serialized text.");
        return text.Remove(last, 1);
    }

    private static string RemovePenultimate(string text, char ch)
    {
        int last = text.LastIndexOf(ch);
        Assert.True(last >= 0, $"Expected '{ch}' in serialized text.");

        int penultimate = text.LastIndexOf(ch, last - 1);
        Assert.True(penultimate >= 0, $"Expected nested '{ch}' in serialized text.");

        return text.Remove(penultimate, 1);
    }
}