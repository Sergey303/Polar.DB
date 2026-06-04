using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-long-lookup",
    Title: "Equal-range lookup by non-unique long external key.",
    Kind: ExperimentKind.ExternalLongLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.LookupWarmupOps,
    MeasuredOps: BenchmarkDefaults.LookupMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
