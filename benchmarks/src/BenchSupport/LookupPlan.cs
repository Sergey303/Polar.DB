namespace PolarDbBenchmarks;

internal sealed record LookupPlan(
    string Name,
    bool FileWarmup,
    object[] WarmupKeys,
    object[] BatchKeys,
    object[] LatencyKeys,
    int BatchSamples,
    int LookupsPerBatchSample)
{
    public LookupPlanManifest ToManifest()
    {
        var expectedBatchQueries = checked(BatchSamples * LookupsPerBatchSample);
        if (BatchKeys.Length != expectedBatchQueries)
            throw new InvalidDataException(
                $"Lookup plan {Name} contains {BatchKeys.Length} batch keys, expected {expectedBatchQueries}.");

        return new LookupPlanManifest(
            Name: Name,
            FileWarmup: FileWarmup,
            WarmupQueries: WarmupKeys.Length,
            MeasuredBatches: BatchSamples,
            QueriesPerBatch: LookupsPerBatchSample,
            BatchQueries: BatchKeys.Length,
            LatencySamples: LatencyKeys.Length,
            TotalMeasuredQueries: checked(BatchKeys.Length + LatencyKeys.Length));
    }
}
