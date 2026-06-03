namespace PolarDbBenchmarks;

internal static class BenchmarkExpected
{
    public static QueryResult ForLookup(ExperimentOptions options, Row[] data)
    {
        ulong checksum = 14695981039346656037UL;
        long rows = 0;
        foreach (var key in BenchmarkData.LookupKeys(data, options.Kind, options.MeasuredOps))
        {
            var matches = LookupMatches(options.Kind, data, key);
            var queryChecksum = BenchmarkChecksum.HashRows(matches);
            checksum = BenchmarkChecksum.Combine(checksum, queryChecksum);
            rows += matches.LongLength;
        }

        return new QueryResult(rows, checksum);
    }

    public static QueryResult ForLifecycle(ExperimentOptions options, Row[] data)
    {
        var expectedRows = options.Kind switch
        {
            ExperimentKind.AppendOnly => data.Concat(
                BenchmarkData.Dataset(options.MeasuredOps, options.Kind, data.Length + 1)),
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
            ExperimentKind.PkLongLookup => data.Where(row => row.LongKey == (long)key).ToArray(),
            ExperimentKind.PkGuidLookup => data.Where(row => row.GuidKey == (string)key).ToArray(),
            ExperimentKind.PkStringLookup => data.Where(row => row.SKey == (string)key).ToArray(),
            ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup =>
                data.Where(row => row.ExternalId == (int)key).ToArray(),
            ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup =>
                data.Where(row => row.ExternalLong == (long)key).ToArray(),
            ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup =>
                data.Where(row => row.ExternalGuid == (string)key).ToArray(),
            ExperimentKind.ExternalStringLookup or ExperimentKind.ExternalFamousStringLookup =>
                data.Where(row => row.ExternalKey == (string)key).ToArray(),
            _ => Array.Empty<Row>()
        };
}
