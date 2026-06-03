using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "pk-long-lookup",
    Title: "Point lookup by unique long primary key.",
    Kind: ExperimentKind.PkLongLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.LookupWarmupOps,
    MeasuredOps: BenchmarkDefaults.LookupMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
