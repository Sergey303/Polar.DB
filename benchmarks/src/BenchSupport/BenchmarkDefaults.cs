namespace PolarDbBenchmarks;

internal static class BenchmarkDefaults
{
    public static readonly int[] RowCounts = { 50_000, 5_000_000 };

    public const int PrimaryColdSamples = 10;
    public const int PrimaryHotSamples = 100;
    public const int PrimaryLookupsPerSample = 100;
    public const int PrimaryWarmupSamples = 3;
    public const int PrimaryLatencySamples = 1_000;

    public const int ExternalColdSamples = 5;
    public const int ExternalHotSamples = 30;
    public const int ExternalTargetRowsPerSample = 20_000;
    public const int ExternalWarmupSamples = 1;
    public const int ExternalLatencySamples = 30;

    public const int FamousColdSamples = 1;
    public const int FamousHotSamples = 1;
    public const int FamousLookupsPerSample = 1;
    public const int FamousWarmupSamples = 1;
    public const int FamousLatencySamples = 1;

    public const int LookupWarmupOps = 0;
    public const int LookupMeasuredOps = 0;
    public const int HeavyExternalWarmupOps = 0;
    public const int HeavyExternalMeasuredOps = 0;

    public const int BuildPrimaryIntWarmupOps = 1;
    public const int BuildPrimaryIntMeasuredOps = 3;

    public const int ReopenWarmupOps = 3;
    public const int ReopenMeasuredOps = 20;

    public const int MutationWarmupOps = 50;
    public const int MutationMeasuredOps = 2_000;
}
