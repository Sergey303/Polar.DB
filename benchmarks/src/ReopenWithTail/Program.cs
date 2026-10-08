using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "reopen-with-tail",
    Title: "Query-ready reopen after rows were appended beyond the persisted snapshot.",
    Kind: ExperimentKind.ReopenWithTail,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.ReopenWarmupOps,
    MeasuredOps: BenchmarkDefaults.ReopenMeasuredOps);

LifecycleBench.Run(options);
