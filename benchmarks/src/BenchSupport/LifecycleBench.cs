using System.Text;

namespace PolarDbBenchmarks;

internal static class LifecycleBench
{
    public static void Run(ExperimentOptions options)
    {
        var work = BenchmarkPaths.PrepareWorkDir(options.ExperimentId);
        var data = BenchmarkData.Dataset(options.SetupRows);
        var expected = BenchmarkExpected.ForLifecycle(options, data);
        var engines = new[]
        {
            SqliteLifecycleEngine.Run(options, data, Path.Combine(work, "sqlite")),
            PolarLifecycleEngine.Run(options, data, Path.Combine(work, "polar"))
        };

        var output = BenchmarkPaths.ResultPath(options.ExperimentId);
        File.WriteAllText(output, BenchmarkReport.Render(options, expected, engines), Encoding.UTF8);
        Console.WriteLine(output);
    }
}
