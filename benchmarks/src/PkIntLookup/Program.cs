using PolarDbBenchmarks;

LookupBench.Run(new LookupOptions(
    ExperimentId: "pk-int-lookup",
    Title: "Point lookup by unique integer primary key.",
    Kind: LookupKind.PrimaryInt,
    SetupRows: 50_000,
    WarmupOps: 300,
    MeasuredOps: 2_000));
