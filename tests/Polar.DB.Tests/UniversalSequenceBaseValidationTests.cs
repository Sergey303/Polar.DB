using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseValidationTests
{
    [Fact]
    public void GetElement_WhenOffsetIsBeforeHeader_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.GetElement(7L));
    }

    [Fact]
    public void GetElement_WhenOffsetEqualsAppendOffset_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.GetElement(sequence.AppendOffset));
    }

    [Fact]
    public void GetTypedElement_WhenTypeIsNull_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentNullException>(() => sequence.GetTypedElement(null!, 8L));
    }

    [Fact]
    public void SetElement_WhenOffsetIsGreaterThanAppendOffset_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.SetElement(20L, sequence.AppendOffset + 1L));
    }

    [Fact]
    public void SetTypedElement_WhenTypeIsNull_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentNullException>(() => sequence.SetTypedElement(null!, 20L, 8L));
    }

    [Fact]
    public void GetByIndex_WhenIndexIsNegative_ThrowsIndexOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<IndexOutOfRangeException>(() => sequence.GetByIndex(-1));
    }

    [Fact]
    public void GetByIndex_WhenIndexEqualsCount_ThrowsIndexOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<IndexOutOfRangeException>(() => sequence.GetByIndex(1));
    }

    [Fact]
    public void GetByIndex_ForVariableSizeSequence_ThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.Flush();

        Assert.Throws<InvalidOperationException>(() => sequence.GetByIndex(0));
    }

    [Fact]
    public void ElementOffset_WhenIndexIsNegative_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.ElementOffset(-1));
    }

    [Fact]
    public void ElementOffset_WhenIndexEqualsCount_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.ElementOffset(1));
    }

    [Fact]
    public void ElementOffset_ForVariableSizeSequence_ThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.Flush();

        Assert.Throws<InvalidOperationException>(() => sequence.ElementOffset(0));
    }

    [Fact]
    public void ElementValues_Range_WhenOffsetIsBeforeHeader_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.ElementValues(7L, 1L).ToArray());
    }

    [Fact]
    public void ElementValues_Range_WhenNumberIsNegative_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.ElementValues(8L, -1L).ToArray());
    }

    [Fact]
    public void ElementOffsetValuePairs_Range_WhenOffsetIsBeforeHeader_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.ElementOffsetValuePairs(7L, 1L).ToArray());
    }

    [Fact]
    public void ElementOffsetValuePairs_Range_WhenNumberIsNegative_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.ElementOffsetValuePairs(8L, -1L).ToArray());
    }

    [Fact]
    public void Scan_WhenHandlerIsNull_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentNullException>(() => sequence.Scan(null!));
    }
}
