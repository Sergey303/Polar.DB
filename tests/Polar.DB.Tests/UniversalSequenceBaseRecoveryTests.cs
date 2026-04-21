using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseRecoveryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void Constructor_ThrowsOnPartialHeader(int headerLength)
    {
        using var stream = new MemoryStream(new byte[headerLength]);

        Assert.Throws<InvalidDataException>(
            () => UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream));

        Assert.Equal(headerLength, stream.Length);
    }

    [Fact]
    public void Constructor_FixedSize_OverdeclaredCount_TruncatesToReadableElements()
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
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(2L, UniversalSequenceBaseTestHelpers.HeaderCount(stream));
        Assert.Equal(100, sequence.GetByIndex(0));
        Assert.Equal(200, sequence.GetByIndex(1));
    }

    [Fact]
    public void Constructor_FixedSize_UnderdeclaredCount_TrimsReadableTailAsGarbage()
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
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(2L, UniversalSequenceBaseTestHelpers.HeaderCount(stream));
        Assert.Equal(10, sequence.GetByIndex(0));
        Assert.Equal(20, sequence.GetByIndex(1));
    }

    [Fact]
    public void Constructor_FixedSize_PartialTrailingBytes_AreTrimmed_And_CountNormalized()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true))
        {
            writer.Write(3L);
            writer.Write(10);
            writer.Write(20);
            writer.Flush();
        }

        UniversalSequenceBaseTestHelpers.AppendRawBytes(stream, 0xAA, 0xBB);

        stream.Position = 0L;
        var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);

        Assert.Equal(2L, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(2L, UniversalSequenceBaseTestHelpers.HeaderCount(stream));
        Assert.Equal(10, sequence.GetByIndex(0));
        Assert.Equal(20, sequence.GetByIndex(1));
    }

    [Fact]
    public void Constructor_VariableSize_UnderdeclaredCount_TrimsReadableTailAsGarbage()
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
        var reopened = new UniversalSequenceBase(personType, stream);

        Assert.Equal(2L, reopened.Count());
        Assert.Equal(expectedAppendOffset, reopened.AppendOffset);
        Assert.Equal(expectedAppendOffset, stream.Length);
        Assert.Equal(2L, UniversalSequenceBaseTestHelpers.HeaderCount(stream));

        var first = Assert.IsType<object[]>(reopened.GetElement(firstOffset));
        var second = Assert.IsType<object[]>(reopened.GetElement(secondOffset));

        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("BB", (string)second[1]);
    }

    [Fact]
    public void Constructor_VariableSize_TruncatedLastRecord_DropsIncompleteTail_And_RecomputesLogicalEnd()
    {
        using var stream = new MemoryStream();
        var personType = UniversalSequenceBaseTestHelpers.CreateVariableSequenceType();
        var sequence = new UniversalSequenceBase(personType, stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long appendOffsetAfterFirst = sequence.AppendOffset;
        sequence.AppendElement(new object[] { 2, "BBBB" });
        sequence.Flush();

        stream.SetLength(stream.Length - 2L);
        stream.Position = 0L;

        var reopened = new UniversalSequenceBase(personType, stream);

        Assert.Equal(1L, reopened.Count());
        Assert.Equal(appendOffsetAfterFirst, reopened.AppendOffset);
        Assert.Equal(appendOffsetAfterFirst, stream.Length);
        Assert.Equal(1L, UniversalSequenceBaseTestHelpers.HeaderCount(stream));

        var first = Assert.IsType<object[]>(reopened.GetElement(firstOffset));
        Assert.Equal(1, (int)first[0]);
        Assert.Equal("A", (string)first[1]);
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
            object? byIndex0 = reopenedSequence.GetByIndex(0);
            Assert.NotNull(byIndex0);
            Assert.Equal(10L, (long)byIndex0);
            object? byIndex1 = reopenedSequence.GetByIndex(1);
            Assert.NotNull(byIndex1);
            Assert.Equal(20L, (long)byIndex1);
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
}
