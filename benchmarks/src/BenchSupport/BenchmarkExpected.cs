namespace PolarDbBenchmarks;

internal static class BenchmarkExpected
{
    public static QueryResult ForLookup(ExperimentKind kind, Row[] data, object[] keys)
    {
        ulong checksum = 14695981039346656037UL;
        long rows = 0;
        foreach (var key in keys)
        {
            var matches = LookupMatches(kind, data, key);
            checksum = BenchmarkChecksum.Combine(checksum, BenchmarkChecksum.HashRows(matches));
            rows += matches.LongLength;
        }

        return new QueryResult(rows, checksum);
    }

    public static QueryResult ForLifecycle(ExperimentOptions options, Row[] data)
    {
        IEnumerable<Row> expectedRows = options.Kind switch
        {
            ExperimentKind.AppendOnly => data.Concat(
                BenchmarkData.Dataset(options.MeasuredOps, options.Kind, data.Length + 1)),
            ExperimentKind.DeleteOnly => data.Skip(options.MeasuredOps),
            ExperimentKind.ReopenWithTail => data.Concat(
                BenchmarkData.Dataset(
                    BenchmarkDefaults.ReopenTailRows,
                    options.Kind,
                    data.LongLength + 1L)),
            _ => data
        };

        var rowCount = options.Kind switch
        {
            ExperimentKind.AppendOnly => checked(data.LongLength + options.MeasuredOps),
            ExperimentKind.DeleteOnly => data.LongLength - options.MeasuredOps,
            ExperimentKind.ReopenWithTail => checked(data.LongLength + BenchmarkDefaults.ReopenTailRows),
            _ => data.LongLength
        };

        return new QueryResult(rowCount, BenchmarkChecksum.HashRows(expectedRows));
    }

    private static Row[] LookupMatches(ExperimentKind kind, Row[] data, object key) =>
        kind switch
        {
            ExperimentKind.PkIntLookup => data.Where(row => row.Id == (long)key).ToArray(),
            ExperimentKind.PkLongLookup => data.Where(row => row.LongKey == (long)key).ToArray(),
            ExperimentKind.PkGuidLookup => data.Where(row => row.GuidKey == (Guid)key).ToArray(),
            ExperimentKind.PkStringLookup => data.Where(row => row.SKey == (string)key).ToArray(),
            ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup =>
                data.Where(row => row.ExternalId == (int)key).ToArray(),
            ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup =>
                data.Where(row => row.ExternalLong == (long)key).ToArray(),
            ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup =>
                data.Where(row => row.ExternalGuid == (Guid)key).ToArray(),
            ExperimentKind.ExternalStringLookup or ExperimentKind.ExternalFamousStringLookup =>
                data.Where(row => row.ExternalKey == (string)key).ToArray(),
            _ => Array.Empty<Row>()
        };
}
