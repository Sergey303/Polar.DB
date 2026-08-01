namespace PolarDbBenchmarks;

public static class LifecycleBench
{
    public static void Run(ExperimentOptions options) => BenchmarkExecution.Run(options);

    internal static BenchmarkWorkerResult RunWorker(
        ExperimentOptions options,
        string runId,
        BenchmarkEngine engine,
        BenchmarkEnvironmentManifest manifest)
    {
        var work = BenchmarkPaths.PrepareEngineWorkDir(options.ExperimentId, runId, engine);
        var runs = new List<BenchmarkRunResult>();

        foreach (var rowCount in options.RowCounts)
        {
            var data = BenchmarkData.Dataset(rowCount, options.Kind);
            var expected = BenchmarkExpected.ForLifecycle(options, data);
            var caseDir = Path.Combine(work, "rows-" + rowCount);
            var engineResult = engine == BenchmarkEngine.Sqlite
                ? SqliteLifecycleEngine.Run(options, data, caseDir)
                : PolarLifecycleEngine.Run(options, data, caseDir);
            runs.Add(new BenchmarkRunResult(rowCount, expected, new[] { engineResult }));
        }

        return new BenchmarkWorkerResult(runId, options.ExperimentId, engine, manifest, runs, null);
    }

    internal static IReadOnlyList<BenchmarkRunResult> Merge(
        BenchmarkWorkerResult sqlite,
        BenchmarkWorkerResult polar)
    {
        var left = sqlite.LifecycleRuns ?? throw new InvalidDataException("SQLite lifecycle result is missing.");
        var right = polar.LifecycleRuns ?? throw new InvalidDataException("Polar.DB lifecycle result is missing.");
        if (left.Count != right.Count) throw new InvalidDataException("Lifecycle row-count sets differ.");

        var merged = new List<BenchmarkRunResult>();
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].SetupRows != right[i].SetupRows || left[i].Expected != right[i].Expected)
                throw new InvalidDataException("Lifecycle runs or expected values differ.");
            merged.Add(new BenchmarkRunResult(
                left[i].SetupRows,
                left[i].Expected,
                left[i].Engines.Concat(right[i].Engines).ToArray()));
        }

        return merged;
    }
}
