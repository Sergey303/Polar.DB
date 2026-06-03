using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseRefreshTests
{
    [Fact]
    public void Refresh_FixedSize_ValidStream_RecomputesAppendOffset_And_KeepsDataAccessible()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(100);
        sequence.AppendElement(200);
        sequence.Flush();

        stream.Position = 0L;
        sequence.Refresh();

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(16L, stream.Position);
        Assert.Equal(100, sequence.GetByIndex(0));
        Assert.Equal(200, sequence.GetByIndex(1));
    }

    [Fact]
    public void Refresh_FixedSize_OverdeclaredCount_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(100);
        sequence.AppendElement(200);
        sequence.Flush();

        UniversalSequenceBaseTestHelpers.WriteHeader(stream, 3L);
        stream.Position = 0L;

        Assert.Throws<InvalidDataException>(() => sequence.Refresh());
    }

    [Fact]
    public void Refresh_FixedSize_UnderdeclaredCount_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();

        UniversalSequenceBaseTestHelpers.WriteHeader(stream, 1L);
        stream.Position = 0L;

        Assert.Throws<InvalidDataException>(() => sequence.Refresh());
    }

    [Fact]
    public void Refresh_FixedSize_PartialTrailingBytes_ThrowInvalidDataException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();

        UniversalSequenceBaseTestHelpers.WriteHeader(stream, 3L);
        UniversalSequenceBaseTestHelpers.AppendRawBytes(stream, 0xAA, 0xBB);
        stream.Position = 0L;

        Assert.Throws<InvalidDataException>(() => sequence.Refresh());
    }

    [Fact]
    public void Refresh_VariableSize_RecomputesLogicalEnd_ForValidStream_And_MovesCursorToAppendOffset()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        long expectedAppendOffset = sequence.AppendOffset;

        stream.Position = 0L;
        sequence.Refresh();

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(expectedAppendOffset, sequence.AppendOffset);
        Assert.Equal(expectedAppendOffset, stream.Position);
        Assert.Equal(expectedAppendOffset, stream.Length);
    }

    [Fact]
    public void Refresh_VariableSize_FullTailAfterDeclaredCount_IsTrimmed_And_HeaderIsRewritten()
    {
        using var stream = new MemoryStream();
        var personType = UniversalSequenceBaseTestHelpers.CreateVariableSequenceType();
        var sequence = new UniversalSequenceBase(personType, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        long expectedAppendOffset = sequence.AppendOffset;

        UniversalSequenceBaseTestHelpers.WriteHeader(stream, 3L);
        UniversalSequenceBaseTestHelpers.AppendSerializedTail(stream, personType, new object[] { 3, "CCC" });

        stream.Position = 0L;
        sequence.Refresh();

        Assert.Equal(3L, sequence.Count());
        Assert.True(sequence.AppendOffset > expectedAppendOffset);
        Assert.Equal(sequence.AppendOffset, stream.Length);
        Assert.Equal(sequence.AppendOffset, stream.Position);

        var first = Assert.IsType<object[]>(sequence.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(sequence.GetElement(secondOffset));

        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BB", (string)second[1]);
    }

    [Fact]
    public void Refresh_VariableSize_ExtraFullTailBeyondDeclaredCount_IsTrimmed_ToLogicalEnd()
    {
        using var stream = new MemoryStream();
        var personType = UniversalSequenceBaseTestHelpers.CreateVariableSequenceType();
        var sequence = new UniversalSequenceBase(personType, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        long expectedAppendOffset = sequence.AppendOffset;

        UniversalSequenceBaseTestHelpers.AppendSerializedTail(stream, personType, new object[] { 3, "CCC" });

        stream.Position = 0L;
        sequence.Refresh();

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(expectedAppendOffset, sequence.AppendOffset);
        Assert.Equal(expectedAppendOffset, stream.Length);
        Assert.Equal(expectedAppendOffset, stream.Position);

        var first = Assert.IsType<object[]>(sequence.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(sequence.GetElement(secondOffset));

        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BB", (string)second[1]);
    }

    [Fact]
    public void Refresh_VariableSize_TruncatedLastRecord_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.AppendElement(new object[] { 2, "BBBB" });
        sequence.Flush();

        stream.SetLength(stream.Length - 2L);
        stream.Position = 0L;

        Assert.Throws<InvalidDataException>(() => sequence.Refresh());
    }

    [Fact]
    public void Refresh_WhenPhysicalStreamBecomesEmpty_ReinitializesEmptySequence()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        sequence.Clear();
        sequence.AppendElement(10L);
        sequence.AppendElement(20L);
        sequence.Flush();

        stream.SetLength(0L);
        stream.Position = 0L;

        sequence.Refresh();

        Assert.Equal(0L, sequence.Count());
        Assert.Equal(8L, sequence.AppendOffset);
        Assert.Equal(8L, stream.Position);
        Assert.Equal(8L, stream.Length);
        Assert.Equal(0L, UniversalSequenceBaseTestHelpers.HeaderCount(stream));
    }
}
