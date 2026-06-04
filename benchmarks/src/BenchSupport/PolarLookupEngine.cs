namespace PolarDbBenchmarks;

internal static class PolarLookupEngine
{
    public static IReadOnlyList<LookupEngineResult> Run(
        ExperimentOptions options,
        Row[] data,
        string dir,
        IReadOnlyList<LookupPlan> plans)
    {
        Directory.CreateDirectory(dir);
        var created = PolarStoreFactory.Open(dir, options.Kind);
        created.Sequence.Load(data.Select(row => PolarRows.ToPolar(row)));
        created.Sequence.Build();
        created.Sequence.Flush();
        created.Sequence.Close();

        var results = new List<LookupEngineResult>();
        foreach (var plan in plans)
            results.Add(RunPhase(options.Kind, dir, plan));

        return results;
    }

    private static LookupEngineResult RunPhase(ExperimentKind kind, string dir, LookupPlan plan)
    {
        if (plan.FileWarmup) BenchmarkFileWarmup.ReadAll(dir);

        var before = BenchmarkResources.Capture();
        var store = PolarStoreFactory.Open(dir, kind);
        store.Sequence.Refresh();
        var session = new PolarLookupSession(store, kind);

        foreach (var key in plan.WarmupKeys)
            session.Query(key);

        var measured = session.Measure(plan);
        store.Sequence.Close();

        return new LookupEngineResult("polar-db-current", "Measured", measured.Samples, plan.MeasuredKeys.Length,
            measured.Rows, measured.Checksum, BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
    }
}
