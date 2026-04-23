# Benchmark Platform Specification

## 1. Purpose

The platform exists not only to measure Polar.DB speed, but to study and compare storage engines along three main axes:

1. correctness and resilience;
2. performance;
3. storage economics.

It must support reproducible experiments, immutable raw results, rerunnable analysis, and future comparison of Polar.DB, SQLite, and other engines.

## 2. Central architectural idea

The center of the system is **not a library**. The center is the **experiment platform**.

- the benchmark platform defines experiments;
- engines implement those experiments;
- results store common metrics and engine-specific diagnostics;
- analysis compares common outcomes;
- charting visualizes trends and comparisons.

## 3. Main principles

1. Raw measurements are immutable.
2. Measurement and evaluation are separated.
3. Experiments are first-class entities.
4. Cross-engine fairness is explicit.
5. Common metrics and engine-specific diagnostics coexist.
6. Reproducibility is mandatory.
7. The platform supports both common and engine-deep experiments.

## 4. Stage-1 project split

- `Polar.DB.Bench.Core`
- `Polar.DB.Bench.Exec`
- `Polar.DB.Bench.Analysis`
- `Polar.DB.Bench.Charts`
- `Polar.DB.Bench.Engine.PolarDb`
- `Polar.DB.Bench.Engine.Sqlite`

## 5. Research frame — 10 mandatory directions

### 5.1. Formalize research questions
Each experiment should support `research` and `hypothesis`.

### 5.2. Separate correctness, performance, and storage economics
Every run should reflect those as distinct dimensions.

### 5.3. Build a workload catalog, not a single benchmark
Workloads should span fixed-size, variable-size, short strings, long strings, sorted, reverse, random, duplicate-heavy, append-heavy, reopen-heavy, mixed, and corruption-oriented cases.

### 5.4. Treat fault injection as a first-class layer
Fault profiles should include truncation, partial headers, stale tails, corrupted counters, missing sidecar state, and interrupted persistence phases.

### 5.5. Capture environment manifests and reproducibility packs
CPU, RAM, OS, runtime, GC mode, filesystem information if known, git commit, seed, engine config, fairness profile, and environment class must be preserved.

### 5.6. Maintain statistical discipline
Support warmups, measured runs, median, p95, min/max, and noise tagging.

### 5.7. Capture explainability metrics
Not only end numbers, but internal counters such as scanned bytes, artifact counts, GC counters, reopen phases, and recalculation costs.

### 5.8. Support asymptotic series
The same workload should run across multiple scales such as 10k, 100k, 1M, 5M, and 10M.

### 5.9. Include semantic benchmarks on top of microbenchmarks
Support lifecycle flows such as load-build-reopen-query, append cycles, corrupt-recover-query, mixed read/write, and restart loops.

### 5.10. Produce research artifacts, not only code
Raw results, analyzed results, charts, markdown summaries, and negative-result notes should all be first-class outputs.

## 6. Polar.DB engine-deep families

### A. State strategy study
Compare sidecar state, derived state, hybrid, and embedded checkpoint approaches.

### B. Refresh / recovery complexity study
Study sensitivity to data size, fixed/variable-size layouts, stale tail length, valid state availability, and damage degree.

### C. AppendOffset invariant study
Measure the cost and value of maintaining strict logical-end invariants.

### D. Duplicate-key lookup study
Study equal-range length, duplicate density, first-match lookup cost, and range traversal cost.

### E. Variable-size rewrite safety/cost study
Study the boundary between acceptable in-place rewrite and append-and-relink approaches.

## 7. Cross-engine architecture — 8 required decisions

### 7.1. `experiment.json` manifest is central
Each experiment is stored as one folder with one main manifest that describes data model, dataset profile, workload, fairness, target matrix, and compare links.

### 7.2. Use engine adapters
Each engine implements a common adapter contract.

### 7.3. Workloads are library-agnostic
Workloads describe semantics, not concrete method names.

### 7.4. Capability model is explicit
Unsupported features are represented explicitly rather than treated as failures.

### 7.5. Common and engine-specific metrics coexist
Common metrics power direct comparisons. Engine diagnostics explain them.

### 7.6. Artifact topology is role-based
The platform models artifact sets, not a single "database file".

### 7.7. Fairness profiles are explicit
Each engine maps the same fairness profile to engine-specific settings.

### 7.8. Support common and engine-deep families
Common experiments are comparable across engines. Engine-deep families explain engine anatomy.

## 8. Result layers

### Raw result
One run equals one immutable raw result file.

### Analyzed result
Analysis reads raw result and writes enriched output. Optional policy/baseline inputs are supported, but they are not the mandatory center of the platform.

### Reports
Charts, markdown reports, HTML dashboards, and comparison summaries are all derived outputs.
