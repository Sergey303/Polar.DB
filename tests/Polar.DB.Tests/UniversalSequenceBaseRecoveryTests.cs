using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseRecoveryTests
{
    [Fact]
    public void Constructor_RecalculatesCountAndAppendOffset_ForFixedSizeStream()
    {
        using var stream = new MemoryStream();

        var writerSequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);
        writerSequence.Clear();
        writerSequence.AppendElement(10L);
        writerSequence.AppendElement(20L);
        writerSequence.AppendElement(30L);
        writerSequence.Flush();

        stream.Position = 1L;

        var reopenedSequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(stream);

        Assert.Equal(3L, reopenedSequence.Count());
        Assert.Equal(32L, reopenedSequence.AppendOffset);
        Assert.Equal(32L, stream.Position);
        Assert.Equal(20L, (long)reopenedSequence.GetByIndex(1));
    }

    [Fact]
    public void Constructor_RecalculatesCountAndAppendOffset_ForVariableSizeStream()
    {
        using var stream = new MemoryStream();

        var writerSequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);
        writerSequence.Clear();
        long firstOffset = writerSequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = writerSequence.AppendElement(new object[] { 2, "BB" });
        writerSequence.Flush();

        long expectedAppendOffset = writerSequence.AppendOffset;
        stream.Position = 2L;

        var reopenedSequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        Assert.Equal(2L, reopenedSequence.Count());
        Assert.Equal(expectedAppendOffset, reopenedSequence.AppendOffset);
        Assert.Equal(expectedAppendOffset, stream.Position);

        var first = Assert.IsType<object[]>(reopenedSequence.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(reopenedSequence.GetElement(secondOffset));

        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BB", (string)second[1]);
    }

    [Fact]
    public void FileBacked_Restart_Preserves_FixedSize_Count_AppendOffset_And_Data()
    {
        using var scope = new UniversalSequenceBaseTestHelpers.TempFileScope("fixed.bin");
        long appendOffsetAfterFlush;

        using (var writerStream = scope.Open())
        {
            var writerSequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(writerStream);
            writerSequence.Clear();
            writerSequence.AppendElement(10L);
            writerSequence.AppendElement(20L);
            writerSequence.Flush();
            appendOffsetAfterFlush = writerSequence.AppendOffset;
        }

        using (var readerStream = scope.Open(FileMode.Open))
        {
            var reopenedSequence = UniversalSequenceBaseTestHelpers.CreateFixedLongSequence(readerStream);

            Assert.Equal(2L, reopenedSequence.Count());
            Assert.Equal(appendOffsetAfterFlush, reopenedSequence.AppendOffset);
            Assert.Equal(10L, (long)reopenedSequence.GetByIndex(0));
            Assert.Equal(20L, (long)reopenedSequence.GetByIndex(1));
        }
    }

    [Fact]
    public void FileBacked_Restart_Preserves_VariableSize_Count_AppendOffset_And_Data()
    {
        using var scope = new UniversalSequenceBaseTestHelpers.TempFileScope("variable.bin");
        long firstOffset;
        long secondOffset;
        long appendOffsetAfterFlush;

        using (var writerStream = scope.Open())
        {
            var writerSequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(writerStream);
            writerSequence.Clear();
            firstOffset = writerSequence.AppendElement(new object[] { 1, "Alice" });
            secondOffset = writerSequence.AppendElement(new object[] { 2, "Bob" });
            writerSequence.Flush();
            appendOffsetAfterFlush = writerSequence.AppendOffset;
        }

        using (var readerStream = scope.Open(FileMode.Open))
        {
            var reopenedSequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(readerStream);

            Assert.Equal(2L, reopenedSequence.Count());
            Assert.Equal(appendOffsetAfterFlush, reopenedSequence.AppendOffset);

            var first = Assert.IsType<object[]>(reopenedSequence.GetElement(firstOffset));
            var second = Assert.IsType<object[]>(reopenedSequence.GetElement(secondOffset));

            Assert.Equal(1, (int)first[0]);
            Assert.Equal("Alice", (string)first[1]);
            Assert.Equal(2, (int)second[0]);
            Assert.Equal("Bob", (string)second[1]);
        }
    }

    [Fact]
    public void AppendOffset_Progression_For_FixedSizeSequence_Is_Append_Flush_Reopen_Stable()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        sequence.Clear();
        Assert.Equal(8L, sequence.AppendOffset);

        sequence.AppendElement(10);
        Assert.Equal(12L, sequence.AppendOffset);

        sequence.AppendElement(20);
        Assert.Equal(16L, sequence.AppendOffset);

        sequence.Flush();
        Assert.Equal(16L, sequence.AppendOffset);

        stream.Position = 0L;
        var reopened = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);
        Assert.Equal(16L, reopened.AppendOffset);
    }

    [Fact]
    public void AppendOffset_Progression_For_VariableSizeSequence_Is_Append_Flush_Reopen_Stable()
    {
        using var stream = new MemoryStream();
        var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);

        sequence.Clear();
        Assert.Equal(8L, sequence.AppendOffset);

        sequence.AppendElement(new object[] { 1, "A" });
        long afterFirstAppend = sequence.AppendOffset;

        sequence.AppendElement(new object[] { 2, "BBBB" });
        long afterSecondAppend = sequence.AppendOffset;

        Assert.True(afterFirstAppend > 8L);
        Assert.True(afterSecondAppend > afterFirstAppend);

        sequence.Flush();
        Assert.Equal(afterSecondAppend, sequence.AppendOffset);

        stream.Position = 0L;
        var reopened = UniversalSequenceBaseTestHelpers.CreateVariableSequence(stream);
        Assert.Equal(afterSecondAppend, reopened.AppendOffset);
    }

    [Fact]
    public void Refresh_RecalculatesVariableSizeTail_AndMovesCursorToAppendOffset()
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
    }

    [Fact]
    public void Refresh_TrimsGarbageTail_ForVariableSizeSequence()
    {
        using var stream = new MemoryStream();
        var personType = UniversalSequenceBaseTestHelpers.CreateVariableSequenceType();
        var sequence = new UniversalSequenceBase(personType, stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        long expectedAppendOffset = sequence.AppendOffset;
        long expectedCount = sequence.Count();

        stream.Position = stream.Length;
        using (var tailWriter = UniversalSequenceBaseTestHelpers.CreateTailWriter(stream))
        {
            ByteFlow.Serialize(tailWriter, new object[] { 3, "CCC" }, personType);
        }

        stream.Position = 0L;
        sequence.Refresh();

        Assert.Equal(expectedCount, sequence.Count());
        Assert.Equal(expectedAppendOffset, sequence.AppendOffset);
        Assert.Equal(expectedAppendOffset, stream.Length);
        Assert.Equal(expectedAppendOffset, stream.Position);
    }

    [Fact]
    public void Refresh_Recalculates_AppendOffset_For_Fixed_Size_Sequence()
    {
        using var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        writer.Write(2L);
        writer.Write(100);
        writer.Write(200);
        writer.Flush();

        stream.Position = 0;

        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(100, sequence.GetByIndex(0));
        Assert.Equal(200, sequence.GetByIndex(1));
    }

    [Fact]
    public void Recovery_DoesNotCountGarbageTail_ForFixedSizeSequence()
    {
        using var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        writer.Write(2L);
        writer.Write(10);
        writer.Write(20);
        writer.Write(30);
        writer.Flush();

        stream.Position = 0L;

        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        long expectedLength = 8L + 2 * sizeof(int);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(expectedLength, sequence.AppendOffset);
        Assert.Equal(expectedLength, stream.Length);
        Assert.Equal(2L, BitConverter.ToInt64(stream.ToArray(), 0));
    }

    [Fact]
    public void Recovery_TrimsTail_ForVariableSizeSequence()
    {
        using var stream = new MemoryStream();
        var personType = UniversalSequenceBaseTestHelpers.CreateVariableSequenceType();
        var sequence = new UniversalSequenceBase(personType, stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();

        long expectedLength = sequence.AppendOffset;

        stream.Position = stream.Length;
        using (var tailWriter = UniversalSequenceBaseTestHelpers.CreateTailWriter(stream))
        {
            ByteFlow.Serialize(tailWriter, new object[] { 3, "CCC" }, personType);
        }

        stream.Position = 0L;
        var reopened = new UniversalSequenceBase(personType, stream);

        Assert.Equal(2L, reopened.Count());
        Assert.Equal(expectedLength, reopened.AppendOffset);
        Assert.Equal(expectedLength, stream.Length);
        Assert.Equal(2L, BitConverter.ToInt64(stream.ToArray(), 0));
    }

    [Fact]
    public void Recovery_TruncatesOverdeclaredCount_ForFixedSizeStream()
    {
        using var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        writer.Write(3L);
        writer.Write(100);
        writer.Write(200);
        writer.Flush();

        stream.Position = 0L;
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(8L + 2 * sizeof(int), sequence.AppendOffset);
        Assert.Equal(2L, BitConverter.ToInt64(stream.ToArray(), 0));
        Assert.Equal(200, sequence.GetByIndex(1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void Constructor_ThrowsOnPartialHeader(int headerLength)
    {
        var stream = new MemoryStream(new byte[headerLength]);

        Assert.Throws<InvalidDataException>(
            () => UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream));

        Assert.Equal(headerLength, stream.Length);
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
        Assert.Equal(0L, BitConverter.ToInt64(stream.ToArray(), 0));
    }
}
