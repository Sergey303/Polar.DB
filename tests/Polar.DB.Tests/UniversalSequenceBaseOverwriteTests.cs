using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseOverwriteTests
{
    [Fact]
    public void FixedSize_InPlace_Overwrite_AtKnownOffset_UpdatesValue_And_PreservesAppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        long secondOffset = sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;
        long lengthBefore = stream.Length;

        sequence.SetElement(200, secondOffset);

        Assert.Equal(3L, sequence.Count());
        Assert.Equal(appendOffsetBefore, sequence.AppendOffset);
        Assert.Equal(lengthBefore, stream.Length);

        Assert.Equal(10, (int)sequence.GetByIndex(0));
        Assert.Equal(200, (int)sequence.GetByIndex(1));
        Assert.Equal(30, (int)sequence.GetByIndex(2));
    }

    [Fact]
    public void FixedSize_SetTypedElement_AtKnownOffset_UpdatesValue_And_PreservesAppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        long secondOffset = sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;
        long lengthBefore = stream.Length;

        sequence.SetTypedElement(new PType(PTypeEnumeration.integer), 200, secondOffset);

        Assert.Equal(3L, sequence.Count());
        Assert.Equal(appendOffsetBefore, sequence.AppendOffset);
        Assert.Equal(lengthBefore, stream.Length);

        Assert.Equal(10, (int)sequence.GetByIndex(0));
        Assert.Equal(200, (int)sequence.GetByIndex(1));
        Assert.Equal(30, (int)sequence.GetByIndex(2));
    }

    [Fact]
    public void FixedSize_AppendAfterOverwrite_StartsAtOriginalLogicalTail()
    {
        using var stream = new MemoryStream();
        var sequence = CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        long secondOffset = sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;

        sequence.SetElement(200, secondOffset);
        long newOffset = sequence.AppendElement(40);
        sequence.Flush();

        Assert.Equal(appendOffsetBefore, newOffset);
        Assert.Equal(4L, sequence.Count());
        Assert.Equal(appendOffsetBefore + sizeof(int), sequence.AppendOffset);

        Assert.Equal(new[] { 10, 200, 30, 40 }, sequence.ElementValues().Cast<int>().ToArray());
    }

    [Fact]
    public void FixedSize_Overwrite_DoesNotShiftOrCorrupt_NeighboringElements()
    {
        using var stream = new MemoryStream();
        var sequence = CreateFixedIntSequence(stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(10);
        long secondOffset = sequence.AppendElement(20);
        long thirdOffset = sequence.AppendElement(30);
        sequence.Flush();

        sequence.SetElement(200, secondOffset);
        sequence.Flush();

        Assert.Equal(8L, firstOffset);
        Assert.Equal(12L, secondOffset);
        Assert.Equal(16L, thirdOffset);

        Assert.Equal(firstOffset, sequence.ElementOffset(0));
        Assert.Equal(secondOffset, sequence.ElementOffset(1));
        Assert.Equal(thirdOffset, sequence.ElementOffset(2));

        Assert.Equal(10, (int)sequence.GetElement(firstOffset));
        Assert.Equal(200, (int)sequence.GetElement(secondOffset));
        Assert.Equal(30, (int)sequence.GetElement(thirdOffset));
    }

    [Fact]
    public void SetElement_BeyondAppendOffset_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => sequence.SetElement(20, sequence.AppendOffset + 1));
    }

    [Fact]
    public void SetTypedElement_BeyondAppendOffset_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => sequence.SetTypedElement(new PType(PTypeEnumeration.integer), 20, sequence.AppendOffset + 1));
    }

    [Fact]
    public void SetElement_AtAppendOffset_AppendsAndAdvancesAppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;
        long lengthBefore = stream.Length;

        sequence.SetElement(30, appendOffsetBefore);
        sequence.Flush();

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(lengthBefore + sizeof(int), stream.Length);
        Assert.Equal(appendOffsetBefore + sizeof(int), sequence.AppendOffset);

        Assert.Equal(10, (int)sequence.GetByIndex(0));
        Assert.Equal(20, (int)sequence.GetByIndex(1));
        Assert.Equal(30, (int)sequence.GetElement(appendOffsetBefore));
    }

    [Fact]
    public void VariableSize_InPlace_Overwrite_ThatCrossesLogicalEnd_Throws_And_Restores_ConsistentState()
    {
        using var stream = new MemoryStream();
        var sequence = CreateVariableSequence(stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "B" });
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;
        long lengthBefore = stream.Length;

        Assert.Throws<InvalidOperationException>(
            () => sequence.SetElement(new object[] { 1, "THIS STRING IS MUCH LONGER THAN BEFORE" }, firstOffset));

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(appendOffsetBefore, sequence.AppendOffset);
        Assert.Equal(lengthBefore, stream.Length);

        var first = Assert.IsType<object[]>(sequence.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(sequence.GetElement(secondOffset));

        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("B", (string)second[1]);
    }

    private static UniversalSequenceBase CreateFixedIntSequence(Stream stream)
    {
        return new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);
    }

    private static UniversalSequenceBase CreateVariableSequence(Stream stream)
    {
        var type = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        return new UniversalSequenceBase(type, stream);
    }
}
