using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteLookupEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);

        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();

        var keys = BenchmarkData.LookupKeys(data, options.Kind, options.MeasuredOps).ToArray();
        for (var i = 0; i < options.WarmupOps; i++)
            Query(connection, options.Kind, keys[i % keys.Length]);

        var samples = new List<double>();
        ulong checksum = 14695981039346656037UL;
        long rows = 0;
        foreach (var key in keys)
        {
            var stopwatch = Stopwatch.StartNew();
            var query = Query(connection, options.Kind, key);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            checksum = BenchmarkChecksum.Combine(checksum, query.Checksum);
            rows += query.Rows;
        }

        return new EngineResult("sqlite", "Measured", samples, rows, checksum,
            BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
    }

    public static QueryResult Query(SqliteConnection connection, ExperimentKind kind, object key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SqliteRows.SelectAllSql() + Where(kind);
        command.Parameters.AddWithValue("$v", key);

        using var reader = command.ExecuteReader();
        var rows = new List<Row>();
        while (reader.Read())
            rows.Add(SqliteRows.Read(reader));

        return new QueryResult(rows.Count, BenchmarkChecksum.HashRows(rows));
    }

    private static string Where(ExperimentKind kind) => kind switch
    {
        ExperimentKind.PkIntLookup => " WHERE id=$v",
        ExperimentKind.PkLongLookup => " WHERE long_key=$v",
        ExperimentKind.PkGuidLookup => " WHERE guid_key=$v",
        ExperimentKind.PkStringLookup => " WHERE skey=$v",
        ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup => " WHERE external_id=$v",
        ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup => " WHERE external_long=$v",
        ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup => " WHERE external_guid=$v",
        _ => " WHERE external_key=$v"
    };
}
