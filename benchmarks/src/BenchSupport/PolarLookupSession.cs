using System.Diagnostics;

namespace PolarDbBenchmarks;

internal sealed class PolarLookupSession
{
    private readonly PolarStore _store;
    private readonly ExperimentKind _kind;

    public PolarLookupSession(PolarStore store, ExperimentKind kind)
    {
        _store = store;
        _kind = kind;
    }

    public QueryResult Query(object key)
    {
        var accumulator = new BenchmarkRowAccumulator();
        long rows = 0;
        foreach (var value in Values((IComparable)key))
        {
            accumulator.Add(PolarRows.FromPolar(value));
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

    private IEnumerable<object> Values(IComparable key)
    {
        if (_kind is ExperimentKind.PkIntLookup or ExperimentKind.PkLongLookup
            or ExperimentKind.PkGuidLookup or ExperimentKind.PkStringLookup)
        {
            var value = _store.Sequence.GetByKey(key);
            if (value != null) yield return value;
            yield break;
        }

        var index = _kind switch
        {
            ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup => _store.IntIndex,
            ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup => _store.LongIndex,
            ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup => _store.GuidIndex,
            _ => _store.StringIndex
        };

        foreach (var value in index!.GetManyByKey(key))
            yield return value;
    }
}
