using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-string-lookup",
    Title: "Equal-range lookup by non-unique string external key.",
    Kind: ExperimentKind.ExternalStringLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.LookupWarmupOps,
    MeasuredOps: BenchmarkDefaults.LookupMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
