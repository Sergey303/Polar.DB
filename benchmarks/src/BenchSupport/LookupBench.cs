using System.Text;

namespace PolarDbBenchmarks;

internal static class LookupBench
{
    public static void Run(ExperimentOptions options)
    {
        var work = BenchmarkPaths.PrepareWorkDir(options.ExperimentId);
        var runs = new List<BenchmarkRunResult>();

        foreach (var rowCount in options.RowCounts)
        {
            var data = BenchmarkData.Dataset(rowCount);
            var caseDir = Path.Combine(work, "rows-" + rowCount);
            var expected = BenchmarkExpected.ForLookup(options, data);
            var engines = new[]
            {
                SqliteLookupEngine.Run(options, data, Path.Combine(caseDir, "sqlite")),
                PolarLookupEngine.Run(options, data, Path.Combine(caseDir, "polar"))
            };
            runs.Add(new BenchmarkRunResult(rowCount, expected, engines));
        }

        var output = BenchmarkPaths.ResultPath(options.ExperimentId);
        File.WriteAllText(output, BenchmarkReport.Render(options, runs), Encoding.UTF8);
        Console.WriteLine(output);
    }
}
