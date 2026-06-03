using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-int-lookup",
    Title: "Equal-range lookup by non-unique integer external key.",
    Kind: ExperimentKind.ExternalIntLookup,
    SetupRows: 50000,
    WarmupOps: 300,
    MeasuredOps: 2000);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
