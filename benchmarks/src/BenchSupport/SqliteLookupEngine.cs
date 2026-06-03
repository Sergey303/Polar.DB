using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteLookupEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);

        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();

        var keys = BenchmarkData.LookupKeys(data, options.Kind, options.MeasuredOps).ToArray();
        for (var i = 0; i < options.WarmupOps; i++)
            Query(connection, options.Kind, keys[i % keys.Length]);

        var samples = new List<double>();
        ulong checksum = 0;
        long rows = 0;
        foreach (var key in keys)
        {
            var stopwatch = Stopwatch.StartNew();
            var query = Query(connection, options.Kind, key);
            stopwatch.Stop();

            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            checksum ^= query.Checksum;
            rows += query.Rows;
        }

        return new EngineResult("sqlite", "Measured", samples, rows, checksum, BenchmarkPaths.DirBytes(dir));
    }

    public static QueryResult Query(SqliteConnection connection, ExperimentKind kind, object key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = kind switch
        {
            ExperimentKind.PkIntLookup => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE id=$v",
            ExperimentKind.PkStringLookup => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE skey=$v",
            ExperimentKind.ExternalIntLookup => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE external_id=$v",
            _ => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE external_key=$v"
        };
        command.Parameters.AddWithValue("$v", key);

        using var reader = command.ExecuteReader();
        ulong checksum = 0;
        long rows = 0;
        while (reader.Read())
        {
            checksum ^= BenchmarkChecksum.Hash(new Row(
                reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4)));
            rows++;
        }

        return new QueryResult(rows, checksum);
    }
}
