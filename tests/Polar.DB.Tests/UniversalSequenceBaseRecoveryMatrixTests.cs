using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Verifies recovery behavior for damaged or inconsistent <see cref="UniversalSequenceBase"/> streams.
/// </summary>
/// <remarks>
/// These tests form the storage corruption matrix: partial headers, overdeclared counts, underdeclared counts,
/// stale tails, and incomplete variable-size records. The common expectation is that recovery must preserve only
/// valid logical items and must normalize count, append offset, and physical stream length consistently.
/// </remarks>
public class UniversalSequenceBaseRecoveryMatrixTests
{
    /// <summary>
    /// Verifies that a stream containing only a partially written sequence header is rejected explicitly.
    /// </summary>
    /// <param name="headerLength">The number of bytes available from the incomplete eight-byte header.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void Constructor_Throws_On_Partial_Header(int headerLength)
    {
        using var stream = new MemoryStream(new byte[headerLength]);

        Assert.Throws<InvalidDataException>(() => StorageCorruptionHelpers.CreateInt32Sequence(stream));
        Assert.Equal(headerLength, stream.Length);
    }

    /// <summary>
    /// Verifies that a fixed-size sequence with a header count larger than the readable payload is normalized downward.
    /// </summary>
    [Fact]
    public void FixedSize_Overdeclared_Count_Is_Normalized_To_Readable_Items()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true))
        {
            writer.Write(3L);
            writer.Write(100);
            writer.Write(200);
            writer.Flush();
        }

        stream.Position = 0L;
        var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(2L, StorageCorruptionHelpers.ReadHeaderCount(stream));
        Assert.Equal(100, sequence.GetByIndex(0));
        Assert.Equal(200, sequence.GetByIndex(1));
    }

    /// <summary>
    /// Verifies that a fixed-size sequence with extra readable bytes after the declared count trims that tail as garbage.
    /// </summary>
    [Fact]
    public void FixedSize_Underdeclared_Count_Trims_Stale_Tail()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true))
        {
            writer.Write(2L);
            writer.Write(10);
            writer.Write(20);
            writer.Write(30);
            writer.Flush();
        }

        stream.Position = 0L;
        var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(10, sequence.GetByIndex(0));
        Assert.Equal(20, sequence.GetByIndex(1));
    }

    /// <summary>
    /// Verifies that incomplete fixed-size trailing bytes are dropped and the header count is rewritten to readable data.
    /// </summary>
    [Fact]
    public void FixedSize_Partial_Trailing_Item_Is_Dropped_And_Header_Is_Normalized()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true))
        {
            writer.Write(3L);
            writer.Write(10);
            writer.Write(20);
            writer.Flush();
        }

        StorageCorruptionHelpers.AppendRawBytes(stream, 0xAA, 0xBB);

        stream.Position = 0L;
        var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(2L, StorageCorruptionHelpers.ReadHeaderCount(stream));
    }

    /// <summary>
    /// Verifies that an extra serialized variable-size record after the declared count is not exposed as logical data.
    /// </summary>
    [Fact]
    public void VariableSize_Underdeclared_Count_Trims_Serialized_Stale_Tail()
    {
        using var stream = new MemoryStream();
        var type = StorageCorruptionHelpers.CreateVariableRecordType();
        var sequence = new UniversalSequenceBase(type, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();
        long expectedAppendOffset = sequence.AppendOffset;

        StorageCorruptionHelpers.AppendSerializedTail(stream, type, new object[] { 3, "CCC" });

        stream.Position = 0L;
        var reopened = new UniversalSequenceBase(type, stream);

        Assert.Equal(2L, reopened.Count());
        Assert.Equal(expectedAppendOffset, reopened.AppendOffset);
        Assert.Equal(expectedAppendOffset, stream.Length);
        Assert.Equal(2L, StorageCorruptionHelpers.ReadHeaderCount(stream));

        var first = Assert.IsType<object[]>(reopened.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(reopened.GetElement(secondOffset));
        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BB", (string)second[1]);
    }

    /// <summary>
    /// Verifies that a truncated final variable-size record is removed from the logical sequence during recovery.
    /// </summary>
    [Fact]
    public void VariableSize_Truncated_Last_Record_Drops_Incomplete_Tail()
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long appendOffsetAfterFirst = sequence.AppendOffset;
        sequence.AppendElement(new object[] { 2, "BBBB" });
        sequence.Flush();

        stream.SetLength(stream.Length - 2L);
        stream.Position = 0L;

        var reopened = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

        Assert.Equal(1L, reopened.Count());
        Assert.Equal(appendOffsetAfterFirst, reopened.AppendOffset);
        Assert.Equal(appendOffsetAfterFirst, stream.Length);

        var first = Assert.IsType<object[]>(reopened.GetElement(firstOffset));
        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
    }
}
