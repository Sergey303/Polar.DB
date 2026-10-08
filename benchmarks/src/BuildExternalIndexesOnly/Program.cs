using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "build-external-indexes-only",
    Title: "Build four typed external indexes over prepared primary storage.",
    Kind: ExperimentKind.BuildExternalIndexesOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.ExternalBuildWarmupOps,
    MeasuredOps: BenchmarkDefaults.ExternalBuildMeasuredOps);

LifecycleBench.Run(options);
