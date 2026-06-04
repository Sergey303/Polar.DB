using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal sealed class SqliteLookupSession : IDisposable
{
    private readonly ExperimentKind _kind;
    private readonly SqliteCommand _command;
    private readonly SqliteParameter _parameter;

    private SqliteLookupSession(ExperimentKind kind, SqliteCommand command, SqliteParameter parameter)
    {
        _kind = kind;
        _command = command;
        _parameter = parameter;
    }

    public static SqliteLookupSession Create(SqliteConnection connection, ExperimentKind kind)
    {
        var command = connection.CreateCommand();
        command.CommandText = SqliteRows.SelectAllSql() + Where(kind);
        var parameter = command.Parameters.Add("$v", SqliteType.Text);
        command.Prepare();
        return new SqliteLookupSession(kind, command, parameter);
    }

    public QueryResult Query(object key)
    {
        _parameter.Value = SqliteKey(_kind, key);
        using var reader = _command.ExecuteReader();
        var accumulator = new BenchmarkRowAccumulator();
        long rows = 0;
        while (reader.Read())
        {
            accumulator.Add(SqliteRows.Read(reader));
            rows++;
        }

        return new QueryResult(rows, accumulator.Finish());
    }

    public (IReadOnlyList<double> Samples, long Rows, ulong Checksum) MeasureBatch(
        LookupPlan plan,
        string progressPrefix)
    {
        var samples = new List<double>();
        ulong checksum = 14695981039346656037UL;
        long rows = 0;
        var offset = 0;
        var progress = new BenchmarkProgress(progressPrefix, plan.BatchSamples);

        for (var sample = 0; sample < plan.BatchSamples; sample++)
        {
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < plan.LookupsPerBatchSample; i++)
            {
                var query = Query(plan.BatchKeys[offset++]);
                checksum = BenchmarkChecksum.Combine(checksum, query.Checksum);
                rows += query.Rows;
            }
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds / plan.LookupsPerBatchSample);
            progress.Step(sample + 1);
        }

        return (samples, rows, checksum);
    }

    public (IReadOnlyList<double> Samples, long Rows, ulong Checksum) MeasureLatency(
        object[] keys,
        string progressPrefix)
    {
        var progress = new BenchmarkProgress(progressPrefix, keys.Length);
        var samples = new List<double>();
        ulong checksum = 14695981039346656037UL;
        long rows = 0;

        for (var i = 0; i < keys.Length; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var query = Query(keys[i]);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            checksum = BenchmarkChecksum.Combine(checksum, query.Checksum);
            rows += query.Rows;
            progress.Step(i + 1);
        }

        return (samples, rows, checksum);
    }

    public void Dispose() => _command.Dispose();

    private static object SqliteKey(ExperimentKind kind, object key) =>
        kind is ExperimentKind.PkGuidLookup or ExperimentKind.ExternalGuidLookup
            or ExperimentKind.ExternalFamousGuidLookup
                ? BenchmarkGuid.ToBytes((Guid)key)
                : key;

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
