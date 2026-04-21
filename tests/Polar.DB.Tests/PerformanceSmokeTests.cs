using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Polar.DB.Tests;

/// <summary>
/// Captures coarse performance smoke measurements for append throughput and reopen/recovery cost.
/// </summary>
/// <remarks>
/// These tests are not strict microbenchmarks. They are intended to make recovery and append costs visible in CI logs
/// while still asserting storage correctness after relatively large operations.
/// </remarks>
public class PerformanceSmokeTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Creates a new performance smoke test instance.
    /// </summary>
    /// <param name="output">The xUnit output helper used to write measurement lines into the test log.</param>
    public PerformanceSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Measures fixed-size append throughput and reopen/recovery cost while verifying durable data correctness.
    /// </summary>
    /// <param name="count">The number of fixed-size items to append before reopening the sequence.</param>
    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void FixedSize_Append_And_Reopen_Smoke_Measures_Throughput_And_Recovery_Cost(int count)
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateInt64Sequence(stream);

        sequence.Clear();
        var appendWatch = Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
            sequence.AppendElement((long)i);
        sequence.Flush();
        appendWatch.Stop();

        long appendOffset = sequence.AppendOffset;
        long streamLength = stream.Length;

        stream.Position = 0L;
        var reopenWatch = Stopwatch.StartNew();
        var reopened = StorageCorruptionHelpers.CreateInt64Sequence(stream);
        reopenWatch.Stop();

        Assert.Equal(count, reopened.Count());
        Assert.Equal(appendOffset, reopened.AppendOffset);
        Assert.Equal(streamLength, stream.Length);
        object? lastItem = reopened.GetByIndex(count - 1);
        Assert.NotNull(lastItem);
        Assert.Equal((long)(count - 1), (long)lastItem);

        _output.WriteLine($"Fixed append: count={count}, elapsedMs={appendWatch.ElapsedMilliseconds}, itemsPerSec={count / Math.Max(0.001, appendWatch.Elapsed.TotalSeconds):F0}");
        _output.WriteLine($"Fixed reopen/recovery: count={count}, elapsedMs={reopenWatch.ElapsedMilliseconds}, bytes={stream.Length}");
    }

    /// <summary>
    /// Measures variable-size append throughput and reopen/recovery cost while checking logical count and append offset.
    /// </summary>
    /// <param name="count">The number of variable-size records to append before reopening the sequence.</param>
    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void VariableSize_Append_And_Reopen_Smoke_Measures_Recovery_Cost(int count)
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

        sequence.Clear();
        var appendWatch = Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
            sequence.AppendElement(new object[] { i, "name-" + i });
        sequence.Flush();
        appendWatch.Stop();

        long appendOffset = sequence.AppendOffset;

        stream.Position = 0L;
        var reopenWatch = Stopwatch.StartNew();
        var reopened = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);
        reopenWatch.Stop();

        Assert.Equal(count, reopened.Count());
        Assert.Equal(appendOffset, reopened.AppendOffset);

        _output.WriteLine($"Variable append: count={count}, elapsedMs={appendWatch.ElapsedMilliseconds}, itemsPerSec={count / Math.Max(0.001, appendWatch.Elapsed.TotalSeconds):F0}");
        _output.WriteLine($"Variable reopen/recovery: count={count}, elapsedMs={reopenWatch.ElapsedMilliseconds}, bytes={stream.Length}");
    }
}
