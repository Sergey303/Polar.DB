using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseLowLevelPrimitiveTests
{
    [Fact]
    public void GetElement_From_Current_Stream_Position_Reads_Current_Record()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.Flush();

        stream.Position = 16L;
        object? element = sequence.GetElement();
        Assert.NotNull(element);
        var value = (long)element;

        Assert.Equal(22L, value);
        Assert.Equal(24L, stream.Position);
    }

    [Fact]
    public void SetElement_At_Current_Stream_Position_Writes_And_Returns_Offset()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(11L);
        sequence.AppendElement(22L);
        sequence.Flush();

        stream.Position = 16L;
        long offset = sequence.SetElement(99L);

        Assert.Equal(16L, offset);
        Assert.Equal(24L, stream.Position);
        Assert.Equal(2L, sequence.Count());
        Assert.Equal(24L, sequence.AppendOffset);
        object? byIndex1 = sequence.GetByIndex(1);
        Assert.NotNull(byIndex1);
        Assert.Equal(99L, (long)byIndex1);
    }
}
