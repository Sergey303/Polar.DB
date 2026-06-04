namespace PolarDbBenchmarks;

internal sealed record LookupPlan(
    string Name,
    bool FileWarmup,
    object[] WarmupKeys,
    object[] BatchKeys,
    object[] LatencyKeys,
    int BatchSamples,
    int LookupsPerBatchSample);
