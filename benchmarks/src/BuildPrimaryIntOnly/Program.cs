using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "build-primary-int-only-id-only-experiment",
    Title: "Build primary integer index only. Experimental id-only storage.",
    Kind: ExperimentKind.BuildPrimaryIntOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.BuildPrimaryIntWarmupOps,
    MeasuredOps: BenchmarkDefaults.BuildPrimaryIntMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
