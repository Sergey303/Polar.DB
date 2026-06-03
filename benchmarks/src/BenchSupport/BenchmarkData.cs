namespace PolarDbBenchmarks;

internal static class BenchmarkData
{
    public static Row[] Dataset(int count, ExperimentKind kind, long startId = 1)
    {
        var rows = new Row[count];
        for (var i = 0; i < count; i++)
        {
            var id = startId + i;
            rows[i] = CreateRow(id, kind);
        }

        return rows;
    }

    public static IEnumerable<object> LookupKeys(Row[] rows, ExperimentKind kind, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var row = rows[i % rows.Length];
            yield return kind switch
            {
                ExperimentKind.PkIntLookup => row.Id,
                ExperimentKind.PkLongLookup => row.LongKey,
                ExperimentKind.PkGuidLookup => row.GuidKey,
                ExperimentKind.PkStringLookup => row.SKey,
                ExperimentKind.ExternalIntLookup => row.ExternalId,
                ExperimentKind.ExternalLongLookup => row.ExternalLong,
                ExperimentKind.ExternalGuidLookup => row.ExternalGuid,
                ExperimentKind.ExternalStringLookup => row.ExternalKey,
                ExperimentKind.ExternalFamousIntLookup => BenchmarkFamousKeys.HitInt,
                ExperimentKind.ExternalFamousLongLookup => BenchmarkFamousKeys.HitLong,
                ExperimentKind.ExternalFamousGuidLookup => BenchmarkFamousKeys.HitGuid,
                ExperimentKind.ExternalFamousStringLookup => BenchmarkFamousKeys.HitString,
                _ => row.Id
            };
        }
    }

    public static IEnumerable<long> PrimaryKeys(Row[] rows, int count)
    {
        for (var i = 0; i < count; i++)
            yield return rows[i % rows.Length].Id;
    }

    private static Row CreateRow(long id, ExperimentKind kind)
    {
        var famous = kind.IsFamousExternal();
        var externalId = famous ? BenchmarkFamousKeys.ExternalInt(id) : (int)(id % 1000);
        var externalLong = famous ? BenchmarkFamousKeys.ExternalLong(id) : 80_000_000_000L + id % 1000;
        var externalGuid = famous ? BenchmarkFamousKeys.ExternalGuid(id) : BenchmarkFamousKeys.GuidFor(2_000_000L + id % 1000);
        var externalKey = famous ? BenchmarkFamousKeys.ExternalString(id) : $"group-{id % 1000:0000}";
        var payload = famous ? BenchmarkFamousKeys.Payload(id) : $"payload-{id:000000000}";

        return new Row(id, 9_000_000_000L + id, BenchmarkFamousKeys.GuidFor(id), $"id-{id:000000000}",
            externalId, externalLong, externalGuid, externalKey, payload);
    }
}
