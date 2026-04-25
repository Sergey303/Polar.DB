using Xunit;

namespace Polar.DB.Tests;

public class CrashRecoveryTests
{
    [Fact]
    public void Constructor_Throws_On_Truncated_Header()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        Assert.Throws<InvalidDataException>(() =>
            new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream));
    }

    [Fact]
    public void Constructor_Trims_Stale_Tail_For_Fixed_Size_Sequence()
    {
        using var stream = new MemoryStream();
        StorageCorruptionHelpers.WriteFixedSequenceBytes(
            stream,
            values: new[] { 10, 20 },
            declaredCount: 2,
            trailingBytes: new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

        var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);

        Assert.Equal(2, sequence.Count());
        // Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(new object[] { 10, 20 }, sequence.ElementValues().ToArray());
    }

    [Fact]
    public void Constructor_Recomputes_Count_When_Fixed_Size_Payload_Is_Truncated()
    {
        using var stream = new MemoryStream();
        StorageCorruptionHelpers.WriteFixedSequenceBytes(
            stream,
            values: new[] { 10, 20 },
            declaredCount: 3);

        var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);

        Assert.Equal(2, sequence.Count());
        Assert.Equal(16L, sequence.AppendOffset);
        Assert.Equal(16L, stream.Length);
        Assert.Equal(new object[] { 10, 20 }, sequence.ElementValues().ToArray());
    }

    [Fact]
    public void Constructor_Trims_Garbage_Tail_For_Variable_Size_Sequence()
    {
        byte[] valid = StorageCorruptionHelpers.BuildVariableStringSequenceBytes("alpha", "beta");
        byte[] corrupted = StorageCorruptionHelpers.ConcatBytes(valid, new byte[] { 0x11, 0x22, 0x33 });

        using var stream = new MemoryStream(corrupted);
        var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), stream);

        Assert.Equal(2, sequence.Count());
        Assert.Equal(valid.Length, sequence.AppendOffset);
        Assert.Equal(valid.Length, stream.Length);
        Assert.Equal(new object[] { "alpha", "beta" }, sequence.ElementValues().ToArray());
    }

    [Fact]
    public void Refresh_Throws_When_Fixed_Size_Header_And_Length_Do_Not_Match()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string path = Path.Combine(tempDir, "sequence.bin");

        try
        {
            var sequence = StorageCorruptionHelpers.CreateIntegerUniversalSequence(path);
            sequence.AppendElement(10);
            sequence.AppendElement(20);
            sequence.Flush();

            StorageCorruptionHelpers.CorruptHeaderCount(path, count: 3);

            var ex = Assert.Throws<InvalidDataException>(() => sequence.Refresh());
            Assert.Contains("fixed-size payload length does not match", ex.Message);
            sequence.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void SetElement_Rolls_Back_When_Variable_Size_Overwrite_Crosses_Logical_End()
    {
        using var stream = new MemoryStream();
        var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), stream);

        sequence.AppendElement("a");
        long secondOffset = sequence.AppendElement("b");
        sequence.Flush();

        long originalLength = stream.Length;
        long originalAppendOffset = sequence.AppendOffset;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            sequence.SetElement("this value is definitely longer than one byte", secondOffset));

        Assert.Contains("crossed the logical end", ex.Message);
        Assert.Equal(originalLength, stream.Length);
        Assert.Equal(originalAppendOffset, sequence.AppendOffset);
        Assert.Equal(2, sequence.Count());
        Assert.Equal("b", (string)sequence.GetElement(secondOffset));
    }

    [Fact]
    public void Constructor_Recovery_Normalizes_String_Sequence_After_Garbage_Tail_Was_Appended_On_Disk()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string path = Path.Combine(tempDir, "sequence.bin");

        try
        {
            var sequence = StorageCorruptionHelpers.CreateStringUniversalSequence(path);
            sequence.AppendElement("alpha");
            sequence.AppendElement("beta");
            sequence.Flush();
            sequence.Close();

            StorageCorruptionHelpers.AppendBytes(path, new byte[] { 0xA0, 0xB1, 0xC2, 0xD3 });

            var reopened = StorageCorruptionHelpers.CreateStringUniversalSequence(path);
            Assert.Equal(2, reopened.Count());
            Assert.Equal(new object[] { "alpha", "beta" }, reopened.ElementValues().ToArray());
            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }
}
