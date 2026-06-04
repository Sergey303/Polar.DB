using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "pk-int-lookup",
    Title: "Point lookup by unique integer primary key.",
    Kind: ExperimentKind.PkIntLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.LookupWarmupOps,
    MeasuredOps: BenchmarkDefaults.LookupMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
