using PolarDbBenchmarks;

var options = new ExperimentOptions(
    ExperimentId: "append-only",
    Title: "Append rows to an existing indexed storage.",
    Kind: ExperimentKind.AppendOnly,
    SetupRows: 50000,
    WarmupOps: 50,
    MeasuredOps: 2000);

if (options.Kind.IsLookup())
    LookupBench.Run(options);
else
    LifecycleBench.Run(options);
