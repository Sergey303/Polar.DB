using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteLifecycleEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind == ExperimentKind.BuildOnly) return BuildOnly(options, data, dir);
        if (options.Kind == ExperimentKind.ReopenOnly) return ReopenOnly(options, data, dir);
        if (options.Kind == ExperimentKind.AppendOnly) return AppendOnly(options, data, dir);
        return DeleteOnly(options, data, dir);
    }

    private static EngineResult BuildOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var samples = new List<double>();
        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            var runDir = Path.Combine(dir, "run-" + i);
            Directory.CreateDirectory(runDir);
            var db = Path.Combine(runDir, "data.sqlite");
            SqliteStore.Create(db, data, withIndexes: false);

            using var connection = new SqliteConnection($"Data Source={db}");
            connection.Open();
            var stopwatch = Stopwatch.StartNew();
            SqliteStore.CreateIndexes(connection);
            stopwatch.Stop();
            if (i >= 0) samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return Result("sqlite", samples, data, dir);
    }

    private static EngineResult ReopenOnly(ExperimentOptions options, Row[] data, string dir)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);
        var samples = new List<double>();

        for (var i = 0; i < options.MeasuredOps + options.WarmupOps; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            using var connection = new SqliteConnection($"Data Source={db}");
            connection.Open();
            stopwatch.Stop();
            if (i >= options.WarmupOps) samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return Result("sqlite", samples, data, dir);
    }

    private static EngineResult AppendOnly(ExperimentOptions options, Row[] data, string dir)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();

        var appendRows = BenchmarkData.Dataset(options.MeasuredOps, data.Length + 1);
        var samples = new List<double>();
        foreach (var row in appendRows)
        {
            var stopwatch = Stopwatch.StartNew();
            SqliteStore.InsertRows(connection, new[] { row });
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return Result("sqlite", samples, data.Concat(appendRows), dir);
    }

    private static EngineResult DeleteOnly(ExperimentOptions options, Row[] data, string dir)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();

        var samples = new List<double>();
        foreach (var key in BenchmarkData.PrimaryKeys(data, options.MeasuredOps))
        {
            var stopwatch = Stopwatch.StartNew();
            Delete(connection, key);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return Result("sqlite", samples, data.Skip(options.MeasuredOps), dir);
    }

    private static void Delete(SqliteConnection connection, long id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rows WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static EngineResult Result(string engine, IReadOnlyList<double> samples, IEnumerable<Row> rows, string dir)
    {
        var materialized = rows.ToArray();
        return new EngineResult(engine, "Measured", samples, materialized.Length,
            BenchmarkChecksum.HashRows(materialized), BenchmarkPaths.DirBytes(dir));
    }
}
