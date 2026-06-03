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
        var before = BenchmarkResources.Capture();
        var samples = new List<double>();
        var artifactDir = dir;
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

            if (i >= 0)
            {
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
                artifactDir = runDir;
            }
        }

        return Result("sqlite", samples, SqliteRows.ReadAll(Path.Combine(artifactDir, "data.sqlite")), artifactDir, before);
    }

    private static EngineResult ReopenOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
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

        return Result("sqlite", samples, SqliteRows.ReadAll(db), dir, before);
    }
private static EngineResult AppendOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();

        var appendRows = BenchmarkData.Dataset(options.MeasuredOps, data.Length + 1);
        var samples = MeasureInTransaction(connection, appendRows, InsertOne);
        return Result("sqlite", samples, SqliteRows.ReadAll(connection), dir, before);
    }

    private static EngineResult DeleteOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();

        var keys = BenchmarkData.PrimaryKeys(data, options.MeasuredOps).ToArray();
        var samples = MeasureInTransaction(connection, keys, DeleteOne);
        return Result("sqlite", samples, SqliteRows.ReadAll(connection), dir, before);
    }

    private static List<double> MeasureInTransaction<T>(
        SqliteConnection connection,
        IEnumerable<T> items,
        Action<SqliteConnection, T> action)
    {
        using var transaction = connection.BeginTransaction();
        var samples = new List<double>();
        foreach (var item in items)
        {
            var stopwatch = Stopwatch.StartNew();
            action(connection, item);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        transaction.Commit();
        return samples;
    }

    private static void InsertOne(SqliteConnection connection, Row row)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO rows(id,skey,external_id,external_key,payload) VALUES($id,$skey,$eid,$ekey,$payload)";
        command.Parameters.AddWithValue("$id", row.Id);
        command.Parameters.AddWithValue("$skey", row.SKey);
        command.Parameters.AddWithValue("$eid", row.ExternalId);
        command.Parameters.AddWithValue("$ekey", row.ExternalKey);
        command.Parameters.AddWithValue("$payload", row.Payload);
        command.ExecuteNonQuery();
    }

    private static void DeleteOne(SqliteConnection connection, long id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rows WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static EngineResult Result(
        string engine,
        IReadOnlyList<double> samples,
        Row[] actualRows,
        string dir,
        ResourceSnapshot before) =>
        new(engine, "Measured", samples, actualRows.Length, BenchmarkChecksum.HashRows(actualRows),
            BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
}
