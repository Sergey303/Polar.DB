namespace PolarDbBenchmarks;

public static class LookupBench
{
    public static void Run(ExperimentOptions options) => BenchmarkExecution.Run(options);

    internal static BenchmarkWorkerResult RunWorker(
        ExperimentOptions options,
        string runId,
        BenchmarkEngine engine,
        BenchmarkEnvironmentManifest manifest)
    {
        var work = BenchmarkPaths.PrepareEngineWorkDir(options.ExperimentId, runId, engine);
        var runs = new List<LookupRunResult>();

        foreach (var rowCount in options.RowCounts)
        {
            BenchmarkProgress.Stage(options.ExperimentId + ": prepare dataset " + rowCount);
            var data = BenchmarkData.Dataset(rowCount, options.Kind);
            var plans = LookupPlanner.Plans(options.Kind, data);
            var caseDir = Path.Combine(work, "rows-" + rowCount);

            var engineResults = engine == BenchmarkEngine.Sqlite
                ? SqliteLookupEngine.Run(options, data, caseDir, plans)
                : PolarLookupEngine.Run(options, data, caseDir, plans);

            runs.Add(new LookupRunResult(
                rowCount,
                BuildSingleEnginePhases(options.Kind, data, plans, engineResults)));
        }

        return new BenchmarkWorkerResult(runId, options.ExperimentId, engine, manifest, null, runs);
    }

    internal static IReadOnlyList<LookupRunResult> Merge(
        BenchmarkWorkerResult sqlite,
        BenchmarkWorkerResult polar)
    {
        var left = sqlite.LookupRuns ?? throw new InvalidDataException("SQLite lookup result is missing.");
        var right = polar.LookupRuns ?? throw new InvalidDataException("Polar.DB lookup result is missing.");
        if (left.Count != right.Count) throw new InvalidDataException("Lookup row-count sets differ.");

        var merged = new List<LookupRunResult>();
        for (var runIndex = 0; runIndex < left.Count; runIndex++)
        {
            var sqliteRun = left[runIndex];
            var polarRun = right[runIndex];
            if (sqliteRun.SetupRows != polarRun.SetupRows ||
                sqliteRun.Phases.Count != polarRun.Phases.Count)
                throw new InvalidDataException("Lookup run shapes differ.");

            var phases = new List<LookupPhaseResult>();
            for (var phaseIndex = 0; phaseIndex < sqliteRun.Phases.Count; phaseIndex++)
            {
                var sqlitePhase = sqliteRun.Phases[phaseIndex];
                var polarPhase = polarRun.Phases[phaseIndex];
                if (sqlitePhase.Name != polarPhase.Name ||
                    sqlitePhase.Expected != polarPhase.Expected)
                    throw new InvalidDataException("Lookup phases or expected values differ.");

                phases.Add(new LookupPhaseResult(
                    sqlitePhase.Name,
                    sqlitePhase.Expected,
                    sqlitePhase.Engines.Concat(polarPhase.Engines).ToArray()));
            }

            merged.Add(new LookupRunResult(sqliteRun.SetupRows, phases));
        }

        return merged;
    }

    private static IReadOnlyList<LookupPhaseResult> BuildSingleEnginePhases(
        ExperimentKind kind,
        Row[] data,
        IReadOnlyList<LookupPlan> plans,
        IReadOnlyList<LookupEngineResult> engineResults)
    {
        if (plans.Count != engineResults.Count)
            throw new InvalidDataException("Lookup plan and engine result counts differ.");

        var phases = new List<LookupPhaseResult>();
        for (var i = 0; i < plans.Count; i++)
        {
            var expected = BenchmarkExpected.ForLookup(kind, data, plans[i].BatchKeys);
            phases.Add(new LookupPhaseResult(
                plans[i].Name,
                expected,
                new[] { engineResults[i] }));
        }

        return phases;
    }
}
