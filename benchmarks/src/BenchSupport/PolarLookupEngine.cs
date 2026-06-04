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
        BenchmarkProgress.Stage(options.ExperimentId + ": polar setup");
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
        if (plan.FileWarmup)
        {
            BenchmarkProgress.Stage("polar-db " + plan.Name + ": file warmup");
            BenchmarkFileWarmup.ReadAll(dir);
        }

        var before = BenchmarkResources.Capture();
        var store = PolarStoreFactory.Open(dir, kind);
        store.Sequence.Refresh();
        var session = new PolarLookupSession(store, kind);

        BenchmarkProgress.Stage("polar-db " + plan.Name + ": lookup warmup " + plan.WarmupKeys.Length);
        foreach (var key in plan.WarmupKeys)
            session.Query(key);

        BenchmarkProgress.Stage("polar-db " + plan.Name + ": batch " + plan.BatchSamples + " samples");
        var batch = session.MeasureBatch(plan, "polar-db " + plan.Name + " batch");

        BenchmarkProgress.Stage("polar-db " + plan.Name + ": latency " + plan.LatencyKeys.Length + " samples");
        var latency = session.MeasureLatency(plan.LatencyKeys, "polar-db " + plan.Name + " latency");
        store.Sequence.Close();

        return new LookupEngineResult("polar-db-current", "Measured", batch.Samples, latency.Samples,
            plan.BatchKeys.Length, batch.Rows, batch.Checksum, latency.Rows, latency.Checksum,
            BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
    }
}
