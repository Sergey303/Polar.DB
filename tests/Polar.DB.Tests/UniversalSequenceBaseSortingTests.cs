using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseSortingTests
{
    [Fact]
    public void Sort32_SortsFixedSizeSequence_InAscendingOrder()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(30L);
        sequence.AppendElement(10L);
        sequence.AppendElement(20L);
        sequence.Flush();

        sequence.Sort32(v =>
        {
            Assert.NotNull(v);
            return checked((int)(long)v);
        });

        Assert.Equal(3L, sequence.Count());
        Assert.Equal(8L + 3L * sizeof(long), sequence.AppendOffset);
            object? byIndex0 = sequence.GetByIndex(0);
            Assert.NotNull(byIndex0);
            Assert.Equal(10L, (long)byIndex0);
            object? byIndex1 = sequence.GetByIndex(1);
            Assert.NotNull(byIndex1);
            Assert.Equal(20L, (long)byIndex1);
            object? byIndex2 = sequence.GetByIndex(2);
            Assert.NotNull(byIndex2);
            Assert.Equal(30L, (long)byIndex2);
    }

    [Fact]
    public void Sort64_SortsFixedSizeSequence_InAscendingOrder()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(300L);
        sequence.AppendElement(100L);
        sequence.AppendElement(200L);
        sequence.Flush();

        sequence.Sort64(v =>
        {
            Assert.NotNull(v);
            return checked((int)(long)v);
        });

        Assert.Equal(3L, sequence.Count());
        Assert.Equal(8L + 3L * sizeof(long), sequence.AppendOffset);
        object? byIndex0 = sequence.GetByIndex(0);
        Assert.NotNull(byIndex0);
        Assert.Equal(100L, (long)byIndex0);
        object? byIndex1 = sequence.GetByIndex(1);
        Assert.NotNull(byIndex1);
        Assert.Equal(200L, (long)byIndex1);
        object? byIndex2 = sequence.GetByIndex(2);
        Assert.NotNull(byIndex2);
        Assert.Equal(300L, (long)byIndex2);
    }

    [Fact]
    public void Sort32_WhenKeySelectorIsNull_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentNullException>(() => sequence.Sort32(null!));
    }

    [Fact]
    public void Sort64_WhenKeySelectorIsNull_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.Flush();

        Assert.Throws<ArgumentNullException>(() => sequence.Sort64(null!));
    }

    [Fact]
    public void Sort32_WhenSequenceHasVariableSizeElements_ThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 2, "B" });
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.Flush();

        Assert.Throws<InvalidOperationException>(() => sequence.Sort32(_ => 0));
    }

    [Fact]
    public void Sort64_WhenSequenceHasVariableSizeElements_ThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 2, "B" });
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.Flush();

        Assert.Throws<InvalidOperationException>(() => sequence.Sort64(_ => 0L));
    }

    [Fact]
    public void Sort32_WhenSingleElementExists_DoesNotChangeSequence()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(42L);
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;

        sequence.Sort32(v =>
        {
            Assert.NotNull(v);
            return checked((int)(long)v);
        });

        Assert.Equal(1L, sequence.Count());
        Assert.Equal(appendOffsetBefore, sequence.AppendOffset);
        object? byIndex0 = sequence.GetByIndex(0);
        
        Assert.NotNull(byIndex0);
        Assert.Equal(42L, (long)byIndex0);
    }

    [Fact]
    public void Sort64_WhenSingleElementExists_DoesNotChangeSequence()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(42L);
        sequence.Flush();

        long appendOffsetBefore = sequence.AppendOffset;

        sequence.Sort64(v =>
        {
            Assert.NotNull(v);
            return checked((int)(long)v);
        });

        Assert.Equal(1L, sequence.Count());
        Assert.Equal(appendOffsetBefore, sequence.AppendOffset);
        object? byIndex0 = sequence.GetByIndex(0);
        
        Assert.NotNull(byIndex0);
        Assert.Equal(42L, (long)byIndex0);
    }
}
