using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "external-string-lookup",
    Title: "Equal-range lookup by non-unique string external key.",
    Kind: ExperimentKind.ExternalStringLookup,
    SetupRows: 50000,
    WarmupOps: 300,
    MeasuredOps: 2000);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
