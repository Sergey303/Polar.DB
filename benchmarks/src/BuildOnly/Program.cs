using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "build-only",
    Title: "Build/index preparation only. Data load is setup.",
    Kind: ExperimentKind.BuildOnly,
    SetupRows: 50000,
    WarmupOps: 1,
    MeasuredOps: 3);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
