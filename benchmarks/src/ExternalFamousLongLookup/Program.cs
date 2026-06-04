using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-famous-long-lookup",
    Title: "Heavy equal-range lookup by famous long external key.",
    Kind: ExperimentKind.ExternalFamousLongLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.HeavyExternalWarmupOps,
    MeasuredOps: BenchmarkDefaults.HeavyExternalMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
