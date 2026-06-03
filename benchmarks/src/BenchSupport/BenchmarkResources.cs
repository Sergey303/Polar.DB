using System.Diagnostics;

namespace PolarDbBenchmarks;

internal static class BenchmarkResources
{
    public static ResourceSnapshot Capture()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        process.Refresh();

        return new ResourceSnapshot(
            GC.GetTotalMemory(forceFullCollection: false),
            process.WorkingSet64,
            process.PrivateMemorySize64,
            BenchmarkSystemMemory.AvailableBytes());
    }
}
