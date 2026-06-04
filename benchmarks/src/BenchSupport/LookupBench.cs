using System.Text;

namespace PolarDbBenchmarks;

internal static class LookupBench
{
    public static void Run(ExperimentOptions options)
    {
        var work = BenchmarkPaths.PrepareWorkDir(options.ExperimentId);
        var runs = new List<LookupRunResult>();

        foreach (var rowCount in options.RowCounts)
        {
            BenchmarkProgress.Stage(options.ExperimentId + ": prepare dataset " + rowCount);
            var data = BenchmarkData.Dataset(rowCount, options.Kind);
            var plans = LookupPlanner.Plans(options.Kind, data);
            var caseDir = Path.Combine(work, "rows-" + rowCount);

            BenchmarkProgress.Stage(options.ExperimentId + ": run sqlite " + rowCount);
            var sqlite = SqliteLookupEngine.Run(options, data, Path.Combine(caseDir, "sqlite"), plans);

            BenchmarkProgress.Stage(options.ExperimentId + ": run polar-db " + rowCount);
            var polar = PolarLookupEngine.Run(options, data, Path.Combine(caseDir, "polar"), plans);

            runs.Add(new LookupRunResult(rowCount, BuildPhases(options.Kind, data, plans, sqlite, polar)));
        }

        var output = BenchmarkPaths.ResultPath(options.ExperimentId);
        File.WriteAllText(output, BenchmarkReport.RenderLookup(options, runs), Encoding.UTF8);
        BenchmarkProgress.Stage(options.ExperimentId + ": report " + output);
    }

    private static IReadOnlyList<LookupPhaseResult> BuildPhases(
        ExperimentKind kind,
        Row[] data,
        IReadOnlyList<LookupPlan> plans,
        IReadOnlyList<LookupEngineResult> sqlite,
        IReadOnlyList<LookupEngineResult> polar)
    {
        var phases = new List<LookupPhaseResult>();
        for (var i = 0; i < plans.Count; i++)
        {
            var expected = BenchmarkExpected.ForLookup(kind, data, plans[i].BatchKeys);
            phases.Add(new LookupPhaseResult(plans[i].Name, expected, new[] { sqlite[i], polar[i] }));
        }

        return phases;
    }
}
