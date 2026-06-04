using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "append-only",
    Title: "Append rows to an existing indexed storage.",
    Kind: ExperimentKind.AppendOnly,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.MutationWarmupOps,
    MeasuredOps: BenchmarkDefaults.MutationMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
