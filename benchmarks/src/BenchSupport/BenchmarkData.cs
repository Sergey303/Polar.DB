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
                ExperimentKind.ExternalStringLookup => row.ExternalKey,
                ExperimentKind.ExternalFamousStringLookup => BenchmarkFamousStrings.HitKey,
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
        var externalKey = kind == ExperimentKind.ExternalFamousStringLookup
            ? BenchmarkFamousStrings.ExternalKey(id)
            : $"group-{id % 1000:0000}";
        var payload = kind == ExperimentKind.ExternalFamousStringLookup
            ? BenchmarkFamousStrings.Payload(id)
            : $"payload-{id:000000000}";

        return new Row(id, 9_000_000_000L + id, GuidFor(id), $"id-{id:000000000}",
            (int)(id % 1000), externalKey, payload);
    }

    private static string GuidFor(long id) =>
        new Guid((int)id, (short)(id >> 32), (short)(id >> 48),
            new byte[] { 1, 2, 3, 4, 5, 6, (byte)(id & 255), (byte)((id >> 8) & 255) })
            .ToString("D");
}
