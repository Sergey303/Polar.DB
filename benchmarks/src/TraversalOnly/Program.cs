using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "traversal-only",
    Title: "Full logical row traversal over prepared storage.",
    Kind: ExperimentKind.TraversalOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.TraversalWarmupOps,
    MeasuredOps: BenchmarkDefaults.TraversalMeasuredOps);

LifecycleBench.Run(options);
