using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseCoreTests
{
    [Fact]
    public void Clear_ResetsState_AndPlacesCursorAtAppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.AppendElement(10L);
        sequence.AppendElement(20L);
        sequence.Flush();

        stream.Position = 0L;
        sequence.Clear();

        Assert.Equal(0L, sequence.Count());
        Assert.Equal(8L, sequence.AppendOffset);
Assert.Equal(8L, stream.Length);
        Assert.Equal(0L, BitConverter.ToInt64(stream.ToArray(), 0));
    }

    [Fact]
    public void Append_Many_FixedSize_Elements_Updates_Count_And_AppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.AppendElement(30);

        Assert.Equal(3L, sequence.Count());
        Assert.Equal(20L, sequence.AppendOffset);
    }

    [Fact]
    public void Append_Many_VariableSize_Elements_Updates_Count_And_AppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "BBBB" });
        sequence.Flush();

        Assert.Equal(2L, sequence.Count());
        Assert.True(firstOffset >= 8L);
        Assert.True(secondOffset > firstOffset);
        Assert.True(sequence.AppendOffset > secondOffset);
    }







    [Fact]
    public void GetByIndex_ReturnsValues_ForFixedSizeSequence()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Flush();

        Assert.Equal(3L, sequence.Count());
        Assert.Equal(10, sequence.GetByIndex(0));
        Assert.Equal(20, sequence.GetByIndex(1));
        Assert.Equal(30, sequence.GetByIndex(2));
    }

    [Fact]
    public void ElementOffset_For_Fixed_Size_Type_Is_Computed_By_Index()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(100);
        sequence.AppendElement(200);
        sequence.Flush();

        Assert.Equal(8L, sequence.ElementOffset(0));
        Assert.Equal(12L, sequence.ElementOffset(1));
        Assert.Equal(16L, sequence.AppendOffset);
    }

    [Fact]
    public void ElementOffset_WithoutArgument_ReturnsAppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        Assert.Equal(sequence.AppendOffset, sequence.ElementOffset());

        sequence.AppendElement(10L);
        Assert.Equal(sequence.AppendOffset, sequence.ElementOffset());
    }

    [Fact]
    public void ElementValues_Returns_All_VariableSize_Elements_In_Order()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        var rows = sequence.ElementValues().Cast<object[]>().ToArray();

        Assert.Equal(2, rows.Length);
        Assert.Equal(1, (int)rows[0][0]);
        Assert.Equal("A", (string)rows[0][1]);
        Assert.Equal(2, (int)rows[1][0]);
        Assert.Equal("BB", (string)rows[1][1]);
    }



    [Fact]
    public void AppendElement_AfterElementOffsetValuePairsEnumeration_AppendsAtLogicalTail()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.AppendElement(20L);
        sequence.Flush();

        stream.Position = 5L;
        _ = sequence.ElementOffsetValuePairs().ToArray();

        long offset = sequence.AppendElement(30L);

        Assert.Equal(24L, offset);
        Assert.Equal(32L, sequence.AppendOffset);
        Assert.Equal(3L, sequence.Count());
Assert.Equal(30L, (long)sequence.GetByIndex(2));
    }
}
