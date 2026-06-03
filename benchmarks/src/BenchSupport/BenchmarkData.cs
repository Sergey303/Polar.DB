namespace PolarDbBenchmarks;

internal static class BenchmarkData
{
    public static Row[] Dataset(int count, long startId = 1)
    {
        var rows = new Row[count];
        for (var i = 0; i < count; i++)
        {
            var id = startId + i;
            rows[i] = new Row(id, $"id-{id:000000000}", (int)(id % 1000),
                $"group-{id % 1000:0000}", $"payload-{id:000000000}");
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
                ExperimentKind.PkStringLookup => row.SKey,
                ExperimentKind.ExternalIntLookup => row.ExternalId,
                ExperimentKind.ExternalStringLookup => row.ExternalKey,
                _ => row.Id
            };
        }
    }

    public static IEnumerable<long> PrimaryKeys(Row[] rows, int count)
    {
        for (var i = 0; i < count; i++)
            yield return rows[i % rows.Length].Id;
    }
}
