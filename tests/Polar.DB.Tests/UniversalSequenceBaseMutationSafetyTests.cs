using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Mutation-safety tests for variable-size sequences.
///
/// These tests intentionally focus on the dangerous gap that still remains in the
/// storage model: in-place overwrite of variable-size elements.
/// Some tests document currently safe behavior; one test fixes the stronger safety
/// contract that the repository may still need to implement.
/// </summary>
public class UniversalSequenceBaseMutationSafetyTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    /// <summary>
    /// Verifies that an in-place rewrite with the same serialized length keeps
    /// subsequent records readable.
    /// </summary>
    [Fact]
    public void SetElement_WithSameSerializedLength_PreservesFollowingRecord()
    {
        using var stream = new MemoryStream();
        var sequence = new UniversalSequenceBase(PersonType, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "AA" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        sequence.SetElement(new object[] { 1, "CC" }, firstOffset);

        var first = Assert.IsType<object[]>(sequence.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(sequence.GetElement(secondOffset));

        Assert.Equal("CC", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BB", (string)second[1]);
    }

    /// <summary>
    /// Verifies the same safe scenario through the typed-write overload.
    /// </summary>
    [Fact]
    public void SetTypedElement_WithSameSerializedLength_PreservesFollowingRecord()
    {
        using var stream = new MemoryStream();
        var sequence = new UniversalSequenceBase(PersonType, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "AA" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        sequence.SetTypedElement(PersonType, new object[] { 1, "DD" }, firstOffset);

        var first = Assert.IsType<object[]>(sequence.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(sequence.GetElement(secondOffset));

        Assert.Equal("DD", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BB", (string)second[1]);
    }

    /// <summary>
    /// Verifies that a variable-size in-place rewrite which grows past the logical
    /// end of the sequence is rejected explicitly.
    /// </summary>
    [Fact]
    public void SetElement_WhenRewriteCrossesLogicalTail_ThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();
        var sequence = new UniversalSequenceBase(PersonType, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        sequence.Flush();

        Assert.Throws<InvalidOperationException>(() =>
            sequence.SetElement(new object[] { 1, new string('X', 128) }, firstOffset));
    }

    /// <summary>
    /// Specification-driving safety test.
    ///
    /// The desired contract is stronger than "throw on overflow":
    /// after a failed in-place rewrite, the sequence should remain readable and keep
    /// its original logical state. If this test is red, it points to a real mutation
    /// safety gap rather than a cosmetic failure.
    /// </summary>
    [Fact]
    public void FailedInPlaceRewrite_ShouldNotMutateExistingSequenceState()
    {
        using var stream = new MemoryStream();
        var sequence = new UniversalSequenceBase(PersonType, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "BBBB" });
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;
        long lengthBefore = stream.Length;
        long countBefore = sequence.Count();

        Assert.Throws<InvalidOperationException>(() =>
            sequence.SetElement(new object[] { 1, new string('X', 512) }, firstOffset));

        Assert.Equal(countBefore, sequence.Count());
        Assert.Equal(appendOffsetBefore, sequence.AppendOffset);
        Assert.Equal(lengthBefore, stream.Length);

        var first = Assert.IsType<object[]>(sequence.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(sequence.GetElement(secondOffset));

        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BBBB", (string)second[1]);
    }
}
