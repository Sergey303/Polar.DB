using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "pk-string-lookup",
    Title: "Point lookup by unique string primary key.",
    Kind: ExperimentKind.PkStringLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.LookupWarmupOps,
    MeasuredOps: BenchmarkDefaults.LookupMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
