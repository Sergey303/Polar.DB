using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "build-primary-int-only",
    Title: "Build primary integer index only. Data load is setup.",
    Kind: ExperimentKind.BuildPrimaryIntOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.BuildPrimaryIntWarmupOps,
    MeasuredOps: BenchmarkDefaults.BuildPrimaryIntMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
