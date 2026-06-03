using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "build-only",
    Title: "Build/index preparation only. Data load is setup.",
    Kind: ExperimentKind.BuildOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.BuildWarmupOps,
    MeasuredOps: BenchmarkDefaults.BuildMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
