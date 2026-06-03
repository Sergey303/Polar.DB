using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-famous-int-lookup",
    Title: "Heavy equal-range lookup by famous integer external key.",
    Kind: ExperimentKind.ExternalFamousIntLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.HeavyExternalWarmupOps,
    MeasuredOps: BenchmarkDefaults.HeavyExternalMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
