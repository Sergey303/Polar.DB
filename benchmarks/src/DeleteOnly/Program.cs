using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "delete-only",
    Title: "Logical delete rows by integer primary key.",
    Kind: ExperimentKind.DeleteOnly,
    SetupRows: 50000,
    WarmupOps: 50,
    MeasuredOps: 2000);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
