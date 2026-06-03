using PolarDbBenchmarks;

LookupBench.Run(new LookupOptions(
    ExperimentId: "pk-string-lookup",
    Title: "Point lookup by unique string primary key.",
    Kind: LookupKind.PrimaryString,
    SetupRows: 50_000,
    WarmupOps: 300,
    MeasuredOps: 2_000));
