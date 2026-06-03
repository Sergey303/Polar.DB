using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-famous-string-lookup",
    Title: "Equal-range lookup by famous string external key with 40 percent matches.",
    Kind: ExperimentKind.ExternalFamousStringLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.HeavyExternalWarmupOps,
    MeasuredOps: BenchmarkDefaults.HeavyExternalMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
