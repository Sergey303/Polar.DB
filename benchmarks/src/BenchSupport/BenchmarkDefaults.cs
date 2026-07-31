namespace PolarDbBenchmarks;

public static class BenchmarkDefaults
{
    public static readonly int[] RowCounts = { 50_000, 5_000_000 };

    public const int PrimaryColdSamples = 30;
    public const int PrimaryHotSamples = 100;
    public const int PrimaryLookupsPerSample = 100;
    public const int PrimaryWarmupSamples = 5;
    public const int PrimaryLatencySamples = 2_000;

    public const int ExternalColdSamples = 15;
    public const int ExternalHotSamples = 30;
    public const int ExternalTargetRowsPerSample = 20_000;
    public const int ExternalWarmupSamples = 3;
    public const int ExternalLatencySamples = 100;

    public const int FamousColdSamples = 5;
    public const int FamousHotSamples = 5;
    public const int FamousLookupsPerSample = 1;
    public const int FamousWarmupSamples = 2;
    public const int FamousLatencySamples = 5;

    public const int LookupWarmupOps = 0;
    public const int LookupMeasuredOps = 0;
    public const int HeavyExternalWarmupOps = 0;
    public const int HeavyExternalMeasuredOps = 0;

    public const int BuildPrimaryIntWarmupOps = 3;
    public const int BuildPrimaryIntMeasuredOps = 15;

    public const int ReopenWarmupOps = 5;
    public const int ReopenMeasuredOps = 30;

    public const int MutationWarmupOps = 200;
    public const int MutationMeasuredOps = 2_000;
    public const int MutationDurableWarmupBatches = 2;
    public const int MutationDurableMeasuredBatches = 15;
    public const int MutationDurableBatchSize = 100;
}
