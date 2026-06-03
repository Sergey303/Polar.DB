using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "pk-string-lookup",
    Title: "Point lookup by unique string primary key.",
    Kind: ExperimentKind.PkStringLookup,
    SetupRows: 50000,
    WarmupOps: 300,
    MeasuredOps: 2000);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
