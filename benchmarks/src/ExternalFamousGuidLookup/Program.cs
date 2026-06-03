using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-famous-guid-lookup",
    Title: "Heavy equal-range lookup by famous GUID external key.",
    Kind: ExperimentKind.ExternalFamousGuidLookup,
    RowCounts: BenchmarkDefaults.RowCounts,
    WarmupOps: BenchmarkDefaults.HeavyExternalWarmupOps,
    MeasuredOps: BenchmarkDefaults.HeavyExternalMeasuredOps);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
