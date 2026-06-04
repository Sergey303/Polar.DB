namespace PolarDbBenchmarks;

internal static class BenchmarkDefaults
{
    public static readonly int[] RowCounts =
    {
        50_000,
        5_000_000
    };

    public const int LookupWarmupOps = 300;
    public const int LookupMeasuredOps = 2_000;

    public const int HeavyExternalWarmupOps = 1;
    public const int HeavyExternalMeasuredOps = 3;

    public const int BuildPrimaryIntWarmupOps = 1;
    public const int BuildPrimaryIntMeasuredOps = 3;

    public const int ReopenWarmupOps = 3;
    public const int ReopenMeasuredOps = 20;

    public const int MutationWarmupOps = 50;
    public const int MutationMeasuredOps = 2_000;
}
