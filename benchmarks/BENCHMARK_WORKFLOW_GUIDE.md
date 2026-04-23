# Benchmark Platform Workflow Guide

This guide explains how to run and interpret benchmarks using the current experiment model:

- one experiment = one folder;
- one experiment = one `experiment.json` manifest;
- all outputs stay inside that experiment folder.

Use this document as the practical onboarding guide for new developers.

## 1. Canonical Experiment Layout

Each experiment lives under:

`benchmarks/experiments/<experiment-slug>/`

Expected structure:

- `experiment.json` - canonical manifest (identity, dataset, workload, engines, compare flags);
- `raw/` - immutable factual run outputs (`*.run.json`);
- `analyzed/` - local interpretation artifacts for this experiment only;
- `comparisons/` - all comparison artifacts and derived expectations;
- `index.html` - human-readable experiment page.

## 2. Artifact Boundaries

Keep these boundaries strict:

- `raw/` = facts from executor, immutable;
- `analyzed/` = local derived artifacts (no cross-object comparisons);
- `comparisons/` = engine comparison, history comparison, cross-experiment context, derived expectations.

Do not place comparison artifacts into `analyzed/`.

## 3. Manifest Basics (`experiment.json`)

Minimal logical blocks:

- `experiment`, `title`, `description`;
- `dataset`, `workload`, `fairness`;
- `engines`;
- `compare.history`;
- `compare.otherExperiments`.

Engine runtime semantics:

- `polar-db` without `nuget` -> current source from this repository;
- `polar-db` with `nuget` -> pinned Polar.DB NuGet version;
- non-Polar engine without `nuget` -> latest NuGet;
- non-Polar engine with `nuget` -> pinned NuGet version.

## 4. End-to-End Flow

### Step 1: Run executor for each engine

Run both engines with the same comparison set id for fair series comparison.

Example:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- \
  --engine polar-db \
  --spec benchmarks/experiments/persons-load-build-reopen-random-lookup \
  --work benchmarks/work/polar-series \
  --comparison-set stage4-load-001

dotnet run --project benchmarks/Polar.DB.Bench.Exec -- \
  --engine sqlite \
  --spec benchmarks/experiments/persons-load-build-reopen-random-lookup \
  --work benchmarks/work/sqlite-series \
  --comparison-set stage4-load-001
```

Executor output is written to:

`benchmarks/experiments/<experiment>/raw/`

Filename pattern:

- single run: `<timestamp>__<engine>.run.json`;
- series run: `<timestamp>__<engine>__<role>-<seq>.run.json`.

### Step 2: Run analysis

Build local analyzed artifacts and comparison artifacts:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Analysis -- \
  --raw-dir benchmarks/experiments/persons-load-build-reopen-random-lookup \
  --compare-experiment persons-load-build-reopen-random-lookup \
  --compare-set stage4-load-001
```

Analysis writes:

- local analyzed snapshots to `analyzed/` (for example `latest-series.polar-db.json`);
- comparison artifacts to `comparisons/`.

### Step 3: Run charts/reporting

Generate markdown/csv summaries and refresh `index.html`:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Charts -- \
  --comparisons benchmarks/experiments/persons-load-build-reopen-random-lookup/comparisons \
  --reports-out benchmarks/experiments/persons-load-build-reopen-random-lookup
```

`index.html` is always generated (no `generateHtml` flag).

## 5. Comparison Artifacts

The `comparisons/` folder should contain:

- `latest-engines.json` - latest successful measured per-engine comparison inside this experiment;
- `latest-history.json` - same-experiment history over time (controlled by `compare.history`);
- `latest-other-experiments.json` - context vs configured other experiments (controlled by `compare.otherExperiments`);
- optional legacy `*.comparison.json` or `*.comparison-series.json` files.

Automatic behavior:

- engine comparison is automatic when multiple engines exist in the manifest;
- history and other-experiments are controlled by manifest flags.

## 6. `index.html` Content Model

The human-readable page should show:

1. experiment identity (title, description, dataset, workload, engines);
2. latest engine comparison;
3. history inside this experiment;
4. cross-experiment context (when enabled);
5. links to machine-readable artifacts from `raw/`, `analyzed/`, `comparisons/`.

## 7. Number Formatting Rules in HTML

Primary display uses scientific notation for large values.

Examples:

- bytes: `157907232` -> `1.579 × 10^8 B (150.6 MiB)`;
- milliseconds: `8421.3` -> `8.421 × 10^3 ms (8.421 s)`.

Guidelines:

- main table cells should be readable;
- exact raw value should remain available via tooltip/title;
- bytes should include binary units (`KiB`, `MiB`, `GiB`).

## 8. Minimum Chart Set

Use simple static charts (inline SVG) and keep implementation maintainable.

Required charts:

1. History chart:
   - x = series/date;
   - y = elapsed median;
   - one line/bar per engine.
2. Phase breakdown chart:
   - load / build / reopen / lookup;
   - latest series per engine.
3. Artifact size chart:
   - primary / side / total bytes;
   - latest series per engine.

Avoid turning reporting into a large frontend app.

## 9. Quick Checklist for New Experiments

1. Create `benchmarks/experiments/<slug>/`.
2. Add `experiment.json`.
3. Create subfolders: `raw/`, `analyzed/`, `comparisons/`.
4. Run executor for configured engines with shared comparison set id.
5. Run analysis.
6. Run charts to regenerate `index.html`.
7. Verify:
   - raw facts are in `raw/`;
   - local interpretation is in `analyzed/`;
   - all comparison artifacts are in `comparisons/`;
   - `index.html` is readable and linked correctly.

## 10. Recommended Reading

For deeper details:

- `benchmarks/BENCHMARK_PLATFORM_SPEC.md`
- `benchmarks/docs/BENCHMARK_METHOD.md`
- `benchmarks/docs/RESULT_SCHEMA.md`
