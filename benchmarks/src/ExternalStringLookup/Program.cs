using PolarDbBenchmarks;

LookupBench.Run(new LookupOptions(
    ExperimentId: "external-string-lookup",
    Title: "Equal-range lookup by non-unique string external key.",
    Kind: LookupKind.ExternalString,
    SetupRows: 50_000,
    WarmupOps: 300,
    MeasuredOps: 2_000));
