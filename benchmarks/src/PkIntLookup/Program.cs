using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "pk-int-lookup",
    Title: "Point lookup by unique integer primary key.",
    Kind: ExperimentKind.PkIntLookup,
    SetupRows: 50000,
    WarmupOps: 300,
    MeasuredOps: 2000);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
