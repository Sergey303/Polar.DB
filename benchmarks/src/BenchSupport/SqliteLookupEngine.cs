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
        SqliteStore.Create(db, data, withIndexes: true);

        var results = new List<LookupEngineResult>();
        foreach (var plan in plans)
            results.Add(RunPhase(options.Kind, db, dir, plan));

        return results;
    }

    private static LookupEngineResult RunPhase(ExperimentKind kind, string db, string dir, LookupPlan plan)
    {
        if (plan.FileWarmup) BenchmarkFileWarmup.ReadAll(dir);

        var before = BenchmarkResources.Capture();
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        using var session = SqliteLookupSession.Create(connection, kind);

        foreach (var key in plan.WarmupKeys)
            session.Query(key);

        var measured = session.Measure(plan);
        return new LookupEngineResult("sqlite", "Measured", measured.Samples, plan.MeasuredKeys.Length,
            measured.Rows, measured.Checksum, BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
    }
}
