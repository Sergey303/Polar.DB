using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "delete-only",
    Title: "Logical delete rows by integer primary key.",
    Kind: ExperimentKind.DeleteOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.MutationWarmupOps,
    MeasuredOps: BenchmarkDefaults.MutationMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
