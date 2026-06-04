namespace PolarDbBenchmarks;

internal sealed record LookupPlan(
    string Name,
    bool FileWarmup,
    object[] WarmupKeys,
    object[] MeasuredKeys,
    int Samples,
    int LookupsPerSample);
