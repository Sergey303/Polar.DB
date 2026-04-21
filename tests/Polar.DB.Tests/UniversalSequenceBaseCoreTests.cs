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
        Assert.Equal(8L, stream.Position);
        Assert.Equal(8L, stream.Length);
        Assert.Equal(0L, BitConverter.ToInt64(stream.ToArray(), 0));
    }

    [Fact]
    public void AppendElement_UsesLogicalTail_UpdatesState_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();

        stream.Position = 0L;
        long firstOffset = sequence.AppendElement(11L);

        Assert.Equal(8L, firstOffset);
        Assert.Equal(1L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(0L, stream.Position);

        stream.Position = 5L;
        long secondOffset = sequence.AppendElement(22L);

        Assert.Equal(16L, secondOffset);
        Assert.Equal(2L, sequence.Count());
        Assert.Equal(24L, sequence.AppendOffset);
        Assert.Equal(5L, stream.Position);
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
    public void Flush_WritesHeader_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.AppendElement(20L);

        stream.Position = 3L;
        sequence.Flush();

        Assert.Equal(3L, stream.Position);
        Assert.Equal(2L, BitConverter.ToInt64(stream.ToArray(), 0));
    }

    [Fact]
    public void Flush_On_EmptySequence_WritesZeroHeader_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();

        stream.Position = 4L;
        sequence.Flush();

        Assert.Equal(4L, stream.Position);
        Assert.Equal(8L, stream.Length);
        Assert.Equal(0L, BitConverter.ToInt64(stream.ToArray(), 0));
        Assert.Equal(8L, sequence.AppendOffset);
    }

    [Fact]
    public void GetElement_ByOffset_ReturnsValue_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.Flush();

        stream.Position = 3L;
        object? element = sequence.GetElement(16L);
        Assert.NotNull(element);
        long value = (long)element;

        Assert.Equal(22L, value);
        Assert.Equal(3L, stream.Position);
    }

    [Fact]
    public void GetTypedElement_ByOffset_ReturnsValue_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(123L);
        sequence.Flush();

        stream.Position = 2L;
        object? typedElement = sequence.GetTypedElement(new PType(PTypeEnumeration.longinteger), 8L);
        Assert.NotNull(typedElement);
        long value = (long)typedElement;

        Assert.Equal(123L, value);
        Assert.Equal(2L, stream.Position);
    }

    [Fact]
    public void GetElement_ByOffset_ReturnsVariableSizeRecord_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        stream.Position = 1L;
        var row = Assert.IsType<object[]>(sequence.GetElement(firstOffset));

        Assert.Equal(1, (int)row[0]);
        Assert.Equal("A", (string)row[1]);
        Assert.Equal(1L, stream.Position);
    }

    [Fact]
    public void SetElement_ByOffset_RewritesExistingValue_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.Flush();

        stream.Position = 1L;
        sequence.SetElement(99L, 16L);

        Assert.Equal(1L, stream.Position);
        Assert.Equal(2L, sequence.Count());
        Assert.Equal(24L, sequence.AppendOffset);
        object? byIndex = sequence.GetByIndex(1);
        Assert.NotNull(byIndex);
        Assert.Equal(99L, (long)byIndex);
    }

    [Fact]
    public void SetTypedElement_RewritesExistingValue_AndRestoresPosition()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.AppendElement(20L);
        sequence.Flush();

        stream.Position = 6L;
        sequence.SetTypedElement(new PType(PTypeEnumeration.longinteger), 77L, 8L);

        Assert.Equal(6L, stream.Position);
        Assert.Equal(2L, sequence.Count());
        Assert.Equal(24L, sequence.AppendOffset);
        object? byIndex0 = sequence.GetByIndex(0);
        Assert.NotNull(byIndex0);
        Assert.Equal(77L, (long)byIndex0);
        object? byIndex1 = sequence.GetByIndex(1);
        Assert.NotNull(byIndex1);
        Assert.Equal(20L, (long)byIndex1);
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
    public void ElementValues_RestoresPosition_AndReturnsAllFixedSizeValues()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.AppendElement(33L);
        sequence.Flush();

        stream.Position = 2L;
        long[] values = sequence.ElementValues().Select(AssertAndUnboxLong).ToArray();

        Assert.Equal(new[] { 11L, 22L, 33L }, values);
        Assert.Equal(2L, stream.Position);
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
    public void ElementValues_Range_RestoresPosition_AndReturnsRequestedValues()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.AppendElement(33L);
        sequence.Flush();

        stream.Position = 4L;
        long[] values = sequence.ElementValues(16L, 2L).Select(AssertAndUnboxLong).ToArray();

        Assert.Equal(new[] { 22L, 33L }, values);
        Assert.Equal(4L, stream.Position);
    }

    [Fact]
    public void ElementOffsetValuePairs_RestoresPosition_AndReturnsOffsetsAndValues()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.AppendElement(33L);
        sequence.Flush();

        stream.Position = 6L;
        var pairs = sequence.ElementOffsetValuePairs().ToArray();

        Assert.Equal(3, pairs.Length);
        Assert.Equal(8L, pairs[0].Item1);
        object? item2 = pairs[0].Item2;
        Assert.NotNull(item2);
        Assert.Equal(11L, (long)item2);
        Assert.Equal(16L, pairs[1].Item1);
        object? item2Second = pairs[1].Item2;
        Assert.NotNull(item2Second);
        Assert.Equal(22L, (long)item2Second);
        Assert.Equal(24L, pairs[2].Item1);
        object? item2Third = pairs[2].Item2;
        Assert.NotNull(item2Third);
        Assert.Equal(33L, (long)item2Third);
        Assert.Equal(6L, stream.Position);
    }

    [Fact]
    public void Scan_RestoresPosition_AndSupportsEarlyStop()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.AppendElement(33L);
        sequence.Flush();

        var readValues = new List<long>();
        var readOffsets = new List<long>();

        stream.Position = 7L;
        sequence.Scan((off, element) =>
        {
            readOffsets.Add(off);
            readValues.Add(AssertAndUnboxLong(element));
            return readValues.Count < 2;
        });

        Assert.Equal(new[] { 8L, 16L }, readOffsets);
        Assert.Equal(new[] { 11L, 22L }, readValues);
        Assert.Equal(7L, stream.Position);
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
        Assert.Equal(5L, stream.Position);
        object? thirdItem = sequence.GetByIndex(2);
        Assert.NotNull(thirdItem);
        Assert.Equal(30L, (long)thirdItem);
    }

    private static long AssertAndUnboxLong(object? value)
    {
        Assert.NotNull(value);
        return (long)value;
    }
}
