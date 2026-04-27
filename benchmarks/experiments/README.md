# Polar.DB Benchmark Experiments

This folder is the executable experiment catalog.

The catalog is derived from a research program, not from the historical list of files. The full measurement program is split into two layers:

1. **Executable now** — experiments that should run with the current benchmark platform.
2. **Planned measurement axes** — scientifically required experiments that must be added only after explicit runner/adapter support exists.

The benchmark platform treats one experiment as one folder with one `experiment.json`. The manifest defines metadata, dataset, workload, targets, fairness constraints, run profile, and comparison settings.

## Research questions

### Q1. Reference lifecycle

Can the engine perform the normal persisted storage lifecycle correctly and efficiently?

Lifecycle:

```text
bulk load -> build/stabilize -> reopen/refresh -> direct lookup -> random point lookup
```

Executable experiments:

- `persons-reference-load-build-reopen-lookup-100k`
- `persons-load-build-reopen-random-lookup`
- `persons-reference-load-build-reopen-lookup-5m`

Why three experiments:

- 100k is a fast smoke/reference run.
- 1m is the main reference baseline.
- 5m is the first scale-pressure point.

### Q2. Lifecycle growth

Does the engine remain correct and stable when the data grows through append and reopen cycles?

Executable experiment:

- `persons-append-cycles-reopen-lookup`

### Q3. Broad current-vs-SQLite adapter coverage

Does current Polar.DB behave competitively against SQLite when a broad capability scenario is executed?

Executable experiment:

- `persons-full-adapter-coverage-version-matrix`

Important: despite the historical folder name, this experiment is now semantically **current Polar.DB vs SQLite only**. It is not a NuGet version matrix.

The name is preserved to avoid breaking existing runner/history assumptions.

### Q4. Polar.DB search diagnostics

How do point lookup, missing-key lookup, and category scan/filter behave across Polar.DB versions?

Executable experiment:

- `polar-db-search-point-and-category-1m`

This is intentionally Polar.DB-only and version-oriented.

### Q5. Historical USequence regression reference

Does current Polar.DB preserve the performance characteristics of an older important USequence benchmark?

Executable experiment:

- `polar-db-u-sequence-reverse-load-build-random-lookup-1m`

This is intentionally Polar.DB-only and version-oriented.

## Strict interpretation rule

Strict performance conclusions may be drawn only between targets inside the same experiment.

Cross-experiment comparison is diagnostic context only.

## Why this folder does not contain fake future experiments

Some important dimensions are not included as `experiment.json` yet:

- cold/warm/hot cache;
- concurrent readers/writers;
- crash recovery and corruption;
- write amplification over long append histories;
- memory-pressure runs;
- larger scale ladder beyond 5m;
- sorted vs reverse vs random load-order matrix;
- missing-key heavy workloads;
- range and secondary-index workloads.

They are documented in `RESEARCH_MATRIX.md` and `PLANNED_EXPERIMENTS.md`, but they should become executable manifests only when the runner supports them explicitly.
