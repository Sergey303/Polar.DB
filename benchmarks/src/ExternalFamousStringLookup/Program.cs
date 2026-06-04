using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-famous-string-lookup",
    Title: "Heavy equal-range lookup by famous string external key.",
    Kind: ExperimentKind.ExternalFamousStringLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.HeavyExternalWarmupOps,
    MeasuredOps: BenchmarkDefaults.HeavyExternalMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
