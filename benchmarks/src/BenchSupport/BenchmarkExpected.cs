namespace PolarDbBenchmarks;

internal static class BenchmarkExpected
{
    public static LookupExpectedOracle CreateLookupOracle(
        ExperimentKind kind,
        Row[] data,
        IReadOnlyList<LookupPlan> plans) =>
        kind switch
        {
            ExperimentKind.PkIntLookup =>
                BuildLookupOracle<long>(data, plans, row => row.Id),
            ExperimentKind.PkLongLookup =>
                BuildLookupOracle<long>(data, plans, row => row.LongKey),
            ExperimentKind.PkGuidLookup =>
                BuildLookupOracle<Guid>(data, plans, row => row.GuidKey),
            ExperimentKind.PkStringLookup =>
                BuildLookupOracle<string>(data, plans, row => row.SKey),
            ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup =>
                BuildLookupOracle<int>(data, plans, row => row.ExternalId),
            ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup =>
                BuildLookupOracle<long>(data, plans, row => row.ExternalLong),
            ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup =>
                BuildLookupOracle<Guid>(data, plans, row => row.ExternalGuid),
            ExperimentKind.ExternalStringLookup or ExperimentKind.ExternalFamousStringLookup =>
                BuildLookupOracle<string>(data, plans, row => row.ExternalKey),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a lookup experiment.")
        };

    public static QueryResult ForLifecycle(ExperimentOptions options, Row[] data)
    {
        if (options.Kind == ExperimentKind.ReopenWithTail)
        {
            var tail = BenchmarkData.Dataset(
                BenchmarkDefaults.ReopenTailRows,
                options.Kind,
                data.LongLength + 1L);
            var ids = data.Select(row => checked((int)row.Id))
                .Concat(tail.Select(row => checked((int)row.Id)));
            return new QueryResult(
                checked(data.LongLength + tail.LongLength),
                BenchmarkChecksum.HashInt32Values(ids));
        }

        IEnumerable<Row> expectedRows = options.Kind switch
        {
            ExperimentKind.AppendOnly => data.Concat(
                BenchmarkData.Dataset(options.MeasuredOps, options.Kind, data.Length + 1)),
            ExperimentKind.DeleteOnly => data.Skip(options.MeasuredOps),
            _ => data
        };

        var rowCount = options.Kind switch
        {
            ExperimentKind.AppendOnly => checked(data.LongLength + options.MeasuredOps),
            ExperimentKind.DeleteOnly => data.LongLength - options.MeasuredOps,
            _ => data.LongLength
        };

        return new QueryResult(rowCount, BenchmarkChecksum.HashRows(expectedRows));
    }

    private static LookupExpectedOracle BuildLookupOracle<TKey>(
        Row[] data,
        IReadOnlyList<LookupPlan> plans,
        Func<Row, TKey> selector)
        where TKey : notnull
    {
        var requested = new HashSet<TKey>();
        foreach (var plan in plans)
        {
            AddRequested(plan.BatchKeys, requested);
            AddRequested(plan.LatencyKeys, requested);
        }

        var groups = new Dictionary<TKey, ExpectedGroup>(requested.Count);
        foreach (var row in data)
        {
            var key = selector(row);
            if (!requested.Contains(key)) continue;

            groups.TryGetValue(key, out var group);
            group.Add(row);
            groups[key] = group;
        }

        return new LookupExpectedOracle(keys =>
        {
            ulong checksum = 14695981039346656037UL;
            long rows = 0;
            foreach (var rawKey in keys)
            {
                var key = (TKey)rawKey;
                groups.TryGetValue(key, out var group);
                checksum = BenchmarkChecksum.Combine(checksum, group.Checksum);
                rows += group.Rows;
            }

            return new QueryResult(rows, checksum);
        });
    }

    private static void AddRequested<TKey>(IEnumerable<object> keys, HashSet<TKey> requested)
        where TKey : notnull
    {
        foreach (var key in keys)
            requested.Add((TKey)key);
    }

    private struct ExpectedGroup
    {
        private BenchmarkRowAccumulator _accumulator;

        public long Rows { get; private set; }

        public ulong Checksum => _accumulator.Finish();

        public void Add(Row row)
        {
            _accumulator.Add(row);
            Rows++;
        }
    }
}

internal sealed class LookupExpectedOracle
{
    private readonly Func<object[], QueryResult> _query;

    public LookupExpectedOracle(Func<object[], QueryResult> query) => _query = query;

    public QueryResult ForKeys(object[] keys) => _query(keys);
}
