using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-guid-lookup",
    Title: "Equal-range lookup by non-unique GUID external key.",
    Kind: ExperimentKind.ExternalGuidLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.LookupWarmupOps,
    MeasuredOps: BenchmarkDefaults.LookupMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
