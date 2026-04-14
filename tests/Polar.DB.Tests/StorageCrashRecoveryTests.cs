using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Simulates crash points around header updates, item writes, and variable-size serialization.
/// </summary>
/// <remarks>
/// These tests use deterministic stream mutations instead of killing a process. The goal is to verify the same durable
/// states a real crash can leave behind while keeping the tests fast and reproducible.
/// </remarks>
public class StorageCrashRecoveryTests
{
    /// <summary>
    /// Verifies recovery after the declared count is incremented but the corresponding item bytes were never written.
    /// </summary>
    [Fact]
    public void Crash_After_Header_Count_Increment_But_Before_Item_Write_Normalizes_To_Readable_Items()
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();

        StorageCorruptionHelpers.WriteHeaderCount(stream, 3L);
        stream.Position = 0L;

        var recovered = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        Assert.Equal(2L, recovered.Count());
        Assert.Equal(10, recovered.GetByIndex(0));
        Assert.Equal(20, recovered.GetByIndex(1));
        Assert.Equal(2L, StorageCorruptionHelpers.ReadHeaderCount(stream));
    }

    /// <summary>
    /// Verifies recovery after item bytes are appended but the header count still describes the previous stable state.
    /// </summary>
    [Fact]
    public void Crash_After_Item_Write_But_Before_Header_Count_Update_Treats_Tail_As_Garbage()
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        sequence.Clear();
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();
        long stableAppendOffset = sequence.AppendOffset;

        StorageCorruptionHelpers.AppendInt32Tail(stream, 30);
        stream.Position = 0L;

        var recovered = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        Assert.Equal(2L, recovered.Count());
        Assert.Equal(stableAppendOffset, recovered.AppendOffset);
        Assert.Equal(stableAppendOffset, stream.Length);
        Assert.Equal(10, recovered.GetByIndex(0));
        Assert.Equal(20, recovered.GetByIndex(1));
    }

    /// <summary>
    /// Verifies recovery after a variable-size item write is interrupted before a complete serialized record exists.
    /// </summary>
    [Fact]
    public void Crash_During_VariableSize_Item_Write_Drops_Incomplete_Record()
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        sequence.AppendElement(new object[] { 2, "BB" });
        sequence.Flush();
        long stableAppendOffset = sequence.AppendOffset;

        StorageCorruptionHelpers.WriteHeaderCount(stream, 3L);
        StorageCorruptionHelpers.AppendRawBytes(stream, 0x01, 0x02, 0x03);
        stream.Position = 0L;

        var recovered = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

        Assert.Equal(2L, recovered.Count());
        Assert.Equal(stableAppendOffset, recovered.AppendOffset);
        Assert.Equal(stableAppendOffset, stream.Length);
    }
}
