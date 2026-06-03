namespace PolarDbBenchmarks;

internal static class BenchmarkExpected
{
    public static QueryResult ForLookup(ExperimentOptions options, Row[] data)
    {
        ulong checksum = 0;
        long rows = 0;
        foreach (var key in BenchmarkData.LookupKeys(data, options.Kind, options.MeasuredOps))
        {
            var matches = LookupMatches(options.Kind, data, key);
            var queryChecksum = BenchmarkChecksum.HashRows(matches);
            checksum ^= queryChecksum;
            rows += matches.LongLength;
        }

        return new QueryResult(rows, checksum);
    }

    public static QueryResult ForLifecycle(ExperimentOptions options, Row[] data)
    {
        var expectedRows = options.Kind switch
        {
            ExperimentKind.AppendOnly => data.Concat(
                BenchmarkData.Dataset(options.MeasuredOps, data.Length + 1)),
            ExperimentKind.DeleteOnly => data.Skip(options.MeasuredOps),
            _ => data
        };

        var materialized = expectedRows.ToArray();
        return new QueryResult(materialized.Length, BenchmarkChecksum.HashRows(materialized));
    }

    private static Row[] LookupMatches(ExperimentKind kind, Row[] data, object key) =>
        kind switch
        {
            ExperimentKind.PkIntLookup => data.Where(row => row.Id == (long)key).ToArray(),
            ExperimentKind.PkStringLookup => data.Where(row => row.SKey == (string)key).ToArray(),
            ExperimentKind.ExternalIntLookup => data.Where(row => row.ExternalId == (int)key).ToArray(),
            ExperimentKind.ExternalStringLookup => data.Where(row => row.ExternalKey == (string)key).ToArray(),
            _ => Array.Empty<Row>()
        };
}
