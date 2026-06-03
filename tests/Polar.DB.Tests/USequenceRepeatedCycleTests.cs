using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Verifies that repeated open, append, flush, close, and reopen cycles do not accumulate storage-state drift.
/// </summary>
/// <remarks>
/// These tests are intentionally file-backed because many sequence-state defects only appear after the stream is
/// disposed and reconstructed repeatedly rather than used once in memory.
/// </remarks>
public class USequenceRepeatedCycleTests
{
    /// <summary>
    /// Verifies that fixed-size data remains ordered and readable after many reopen-and-append cycles.
    /// </summary>
    [Fact]
    public void FixedSize_Repeated_Reopen_Append_Recover_Cycles_Preserve_Count_And_Data()
    {
        using var temp = new StorageCorruptionHelpers.TempDirectory();

        for (int cycle = 0; cycle < 100; cycle++)
        {
            using var stream = temp.Open("fixed-cycle.bin");
            var sequence = StorageCorruptionHelpers.CreateInt64Sequence(stream);

            if (cycle == 0)
                sequence.Clear();

            sequence.AppendElement((long)cycle);
            sequence.Flush();
        }

        using var finalStream = temp.Open("fixed-cycle.bin", FileMode.Open);
        var reopened = StorageCorruptionHelpers.CreateInt64Sequence(finalStream);

        Assert.Equal(100L, reopened.Count());
        for (int i = 0; i < 100; i++)
            Assert.Equal(i, (long)reopened.GetByIndex(i));
    }

    /// <summary>
    /// Verifies that variable-size data preserves count, append offset, stream length, and payloads across reopen cycles.
    /// </summary>
    [Fact]
    public void VariableSize_Repeated_Reopen_Append_Recover_Cycles_Preserve_Count_AppendOffset_And_Data()
    {
        using var temp = new StorageCorruptionHelpers.TempDirectory();
        long lastAppendOffset = 0L;

        for (int cycle = 0; cycle < 50; cycle++)
        {
            using var stream = temp.Open("variable-cycle.bin");
            var sequence = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

            if (cycle == 0)
                sequence.Clear();

            sequence.AppendElement(new object[] { cycle, "name-" + cycle });
            sequence.Flush();
            lastAppendOffset = sequence.AppendOffset;
        }

        using var finalStream = temp.Open("variable-cycle.bin", FileMode.Open);
        var reopened = StorageCorruptionHelpers.CreateVariableRecordSequence(finalStream);

        Assert.Equal(50L, reopened.Count());
        Assert.Equal(lastAppendOffset, reopened.AppendOffset);
        Assert.Equal(lastAppendOffset, finalStream.Length);

        int expected = 0;

        foreach (object raw in reopened.ElementValues())
        {
            var item = Assert.IsType<object[]>(raw);
            Assert.Equal(expected, (int)item[0]);
            Assert.Equal("name-" + expected, (string)item[1]);
            expected++;
        }

        Assert.Equal(50, expected);
    }
}
