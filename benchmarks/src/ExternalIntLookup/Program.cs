using PolarDbBenchmarks;

LookupBench.Run(new LookupOptions(
    ExperimentId: "external-int-lookup",
    Title: "Equal-range lookup by non-unique integer external key.",
    Kind: LookupKind.ExternalInt,
    SetupRows: 50_000,
    WarmupOps: 300,
    MeasuredOps: 2_000));
