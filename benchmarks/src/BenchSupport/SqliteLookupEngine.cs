using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteLookupEngine
{
    public static IReadOnlyList<LookupEngineResult> Run(
        ExperimentOptions options,
        Row[] data,
        string dir,
        IReadOnlyList<LookupPlan> plans)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        BenchmarkProgress.Stage(options.ExperimentId + ": sqlite setup");
        SqliteStore.Create(db, data, withIndexes: true);

        var results = new List<LookupEngineResult>();
        foreach (var plan in plans)
            results.Add(RunPhase(options.Kind, db, dir, plan));

        return results;
    }

    private static LookupEngineResult RunPhase(ExperimentKind kind, string db, string dir, LookupPlan plan)
    {
        if (plan.FileWarmup)
        {
            BenchmarkProgress.Stage("sqlite " + plan.Name + ": file warmup");
            BenchmarkFileWarmup.ReadAll(dir);
        }

        var before = BenchmarkResources.Capture();
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        using var session = SqliteLookupSession.Create(connection, kind);

        BenchmarkProgress.Stage("sqlite " + plan.Name + ": lookup warmup " + plan.WarmupKeys.Length);
        foreach (var key in plan.WarmupKeys)
            session.Query(key);

        BenchmarkProgress.Stage("sqlite " + plan.Name + ": batch " + plan.BatchSamples + " samples");
        var batch = session.MeasureBatch(plan, "sqlite " + plan.Name + " batch");

        BenchmarkProgress.Stage("sqlite " + plan.Name + ": latency " + plan.LatencyKeys.Length + " samples");
        var latency = session.MeasureLatency(plan.LatencyKeys, "sqlite " + plan.Name + " latency");

        return new LookupEngineResult("sqlite", "Measured", batch.Samples, latency.Samples,
            plan.BatchKeys.Length, batch.Rows, batch.Checksum, latency.Rows, latency.Checksum,
            BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
    }
}
