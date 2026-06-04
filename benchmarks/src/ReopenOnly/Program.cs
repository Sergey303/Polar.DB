using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "reopen-only",
    Title: "Reopen existing prepared storage only.",
    Kind: ExperimentKind.ReopenOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.ReopenWarmupOps,
    MeasuredOps: BenchmarkDefaults.ReopenMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
