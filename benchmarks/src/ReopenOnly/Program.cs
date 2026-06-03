using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "reopen-only",
    Title: "Reopen existing prepared storage only.",
    Kind: ExperimentKind.ReopenOnly,
    SetupRows: 50000,
    WarmupOps: 3,
    MeasuredOps: 20);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
