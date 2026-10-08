using System.Diagnostics;

namespace PolarDbBenchmarks;

internal readonly record struct OperationMeasurement(
    double ElapsedMs,
    long AllocatedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);

internal static class BenchmarkMeasure
{
    public static OperationMeasurement Profile(Action action)
    {
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);

        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();

        return new OperationMeasurement(
            stopwatch.Elapsed.TotalMilliseconds,
            GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore,
            GC.CollectionCount(0) - gen0Before,
            GC.CollectionCount(1) - gen1Before,
            GC.CollectionCount(2) - gen2Before);
    }
}

internal sealed class MutableAllocationGcSamples
{
    private readonly List<long> _allocated = new();
    private readonly List<int> _gen0 = new();
    private readonly List<int> _gen1 = new();
    private readonly List<int> _gen2 = new();

    public void Add(OperationMeasurement measurement)
    {
        _allocated.Add(measurement.AllocatedBytes);
        _gen0.Add(measurement.Gen0Collections);
        _gen1.Add(measurement.Gen1Collections);
        _gen2.Add(measurement.Gen2Collections);
    }

    public AllocationGcSamples ToImmutable() =>
        new(_allocated, _gen0, _gen1, _gen2);
}
