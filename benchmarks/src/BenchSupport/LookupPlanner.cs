namespace PolarDbBenchmarks;

internal static class LookupPlanner
{
    public static IReadOnlyList<LookupPlan> Plans(ExperimentKind kind, Row[] rows)
    {
        var lookups = LookupsPerSample(kind, rows);
        return new[]
        {
            new LookupPlan("Cold after reopen", false, Array.Empty<object>(),
                Keys(kind, rows, ColdSamples(kind) * lookups, 17), ColdSamples(kind), lookups),
            new LookupPlan("Hot after file and lookup warmup", true,
                Keys(kind, rows, WarmupSamples(kind) * lookups, 71),
                Keys(kind, rows, HotSamples(kind) * lookups, 97), HotSamples(kind), lookups)
        };
    }

    private static int LookupsPerSample(ExperimentKind kind, Row[] rows)
    {
        if (kind.IsPrimaryLookup()) return BenchmarkDefaults.PrimaryLookupsPerSample;
        if (kind.IsFamousExternal()) return BenchmarkDefaults.FamousLookupsPerSample;

        var perQuery = Math.Max(1, RowsPerQuery(kind, rows));
        return Math.Max(1, BenchmarkDefaults.ExternalTargetRowsPerSample / perQuery);
    }

    private static int RowsPerQuery(ExperimentKind kind, Row[] rows)
    {
        var key = KeyAt(kind, rows, 0);
        return kind switch
        {
            ExperimentKind.ExternalIntLookup => rows.Count(row => row.ExternalId == (int)key),
            ExperimentKind.ExternalLongLookup => rows.Count(row => row.ExternalLong == (long)key),
            ExperimentKind.ExternalGuidLookup => rows.Count(row => row.ExternalGuid == (Guid)key),
            _ => rows.Count(row => row.ExternalKey == (string)key)
        };
    }

    private static int ColdSamples(ExperimentKind kind) =>
        kind.IsPrimaryLookup() ? BenchmarkDefaults.PrimaryColdSamples :
        kind.IsFamousExternal() ? BenchmarkDefaults.FamousColdSamples :
        BenchmarkDefaults.ExternalColdSamples;

    private static int HotSamples(ExperimentKind kind) =>
        kind.IsPrimaryLookup() ? BenchmarkDefaults.PrimaryHotSamples :
        kind.IsFamousExternal() ? BenchmarkDefaults.FamousHotSamples :
        BenchmarkDefaults.ExternalHotSamples;

    private static int WarmupSamples(ExperimentKind kind) =>
        kind.IsPrimaryLookup() ? BenchmarkDefaults.PrimaryWarmupSamples :
        kind.IsFamousExternal() ? BenchmarkDefaults.FamousWarmupSamples :
        BenchmarkDefaults.ExternalWarmupSamples;

    private static object[] Keys(ExperimentKind kind, Row[] rows, int count, int seed)
    {
        var keys = new object[count];
        for (var i = 0; i < count; i++)
            keys[i] = KeyAt(kind, rows, i + seed);
        return keys;
    }

    private static object KeyAt(ExperimentKind kind, Row[] rows, int index)
    {
        var row = rows[(int)(((long)index * 1_000_003L + 97) % rows.Length)];
        return kind switch
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
