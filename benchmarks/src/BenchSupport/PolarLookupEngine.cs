using System.Diagnostics;

namespace PolarDbBenchmarks;

internal static class PolarLookupEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        Directory.CreateDirectory(dir);
        var created = PolarStoreFactory.Open(dir, options.Kind);
        created.Sequence.Load(data.Select(row => PolarRows.ToPolar(row)));
        created.Sequence.Build();
        created.Sequence.Flush();
        created.Sequence.Close();

        var store = PolarStoreFactory.Open(dir, options.Kind);
        store.Sequence.Refresh();

        var keys = BenchmarkData.LookupKeys(data, options.Kind, options.MeasuredOps).ToArray();
        for (var i = 0; i < options.WarmupOps; i++)
            Query(store, options.Kind, keys[i % keys.Length]);

        var samples = new List<double>();
        ulong checksum = 14695981039346656037UL;
        long rows = 0;
        foreach (var key in keys)
        {
            var stopwatch = Stopwatch.StartNew();
            var query = Query(store, options.Kind, key);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            checksum = BenchmarkChecksum.Combine(checksum, query.Checksum);
            rows += query.Rows;
        }

        store.Sequence.Close();
        return new EngineResult("polar-db-current", "Measured", samples, rows,
            checksum, BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
    }

    public static QueryResult Query(PolarStore store, ExperimentKind kind, object key)
    {
        var rows = Values(store, kind, (IComparable)key).Select(PolarRows.FromPolar).ToArray();
        return new QueryResult(rows.Length, BenchmarkChecksum.HashRows(rows));
    }

    private static IEnumerable<object> Values(PolarStore store, ExperimentKind kind, IComparable key)
    {
        if (kind is ExperimentKind.PkIntLookup or ExperimentKind.PkLongLookup
            or ExperimentKind.PkGuidLookup or ExperimentKind.PkStringLookup)
        {
            var value = store.Sequence.GetByKey(key);
            if (value != null) yield return value;
            yield break;
        }

        var index = kind switch
        {
            ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup => store.IntIndex,
            ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup => store.LongIndex,
            ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup => store.GuidIndex,
            _ => store.StringIndex
        };

        foreach (var value in index!.GetManyByKey(key))
            yield return value;
    }
}
