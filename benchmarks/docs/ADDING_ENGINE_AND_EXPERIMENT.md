# Adding Engines and Experiments

## How to Add a New Target for an Existing Library

A target is a runtime variant of an engine family. To add a new target (e.g. `polar-db-2.2.0`):

1. **Add the target to experiment.json**:
   ```json
   "targets": {
     "polar-db-2.2.0": {
       "engine": "polar-db",
       "nuget": "2.2.0"
     }
   }
   ```

2. **Create a typed runner project** (if needed):
   - Copy an existing typed runner (e.g. `Polar.DB.Bench.Exec.PolarDb210`)
   - Update the NuGet package reference in the `.csproj`
   - Update `RunnerIdentity`, `RuntimeSource`, `RuntimeNuget` constants
   - Keep the workload logic **identical** to other targets

3. **Register the runner** in `ExecApplication.cs`:
   - Add the target key to `ResolveTypedPolarDbRunnerProjectPath`
   - Add the project name mapping

## How to Add a New Library (Engine Family)

To add a completely new engine (e.g. `my-engine`):

1. **Create an adapter** implementing `IStorageEngineAdapter` in a new project under `benchmarks/src/`
2. **Register the adapter** in `ExecApplication.CreateAdapter()`
3. **Add targets** to experiment.json with `"engine": "my-engine"`
4. **Ensure fairness**: the same workload semantics must apply across all engines

## How to Add a New Experiment

1. **Create a folder** under `benchmarks/experiments/<experiment-key>/`
2. **Create `experiment.json`** with all required fields:

   ```json
   {
     "schema": "polar-bench-experiment/v1",
     "protocol": "<protocol-id>/v1",
     "experiment": "<unique-key>",
     "title": "Human-readable title",
     "research": "<research-question-id>",
     "hypothesis": "<hypothesis-id>",
     "description": "What this experiment measures and why",
     "dataset": {
       "profile": "<profile-key>",
       "count": 1000000
     },
     "workload": {
       "type": "<workload-type>",
       "lookup": 10000,
       "options": { ... }
     },
     "fairness": {
       "type": "<fairness-profile>",
       "notes": "All targets run the same scenario"
     },
     "runs": {
       "warmup": 5,
       "measured": 50,
       "notes": "Scientific profile"
     },
     "targets": {
       "target-key-1": { "engine": "polar-db" },
       "target-key-2": { "engine": "polar-db", "nuget": "2.1.1" }
     },
     "compare": {
       "history": true,
       "otherExperiments": false
     }
   }
   ```

3. **Run the experiment**:
   ```
   dotnet run --project ...\Polar.DB.Bench.Exec.csproj -- --exp ...\experiment-folder
   ```

## What Must Be Described in experiment.json

- `experiment` — unique key (must match folder name)
- `title` — human-readable
- `dataset` — profile + count
- `workload` — type + parameters
- `fairness` — how fairness is ensured across targets
- `targets` — at least one target
- `runs` — recommended (warmup + measured counts)

Optional but recommended:
- `schema` — version identifier
- `protocol` — protocol identifier
- `research`, `hypothesis` — for traceability
- `description` — detailed intent

## Search Experiment Guidelines

When adding a search experiment, describe the following in `experiment.json`:

- **Query type**: Specify whether the workload is point lookup (single-key GetByKey), multi-key lookup, or scan/filter query.
- **Expected result cardinality**: Document how many results each query type returns (e.g., 1 for point lookup, N for scan).
- **Cache**: State whether cache is enabled or disabled. If cache is intentionally not measured, note this explicitly.
- **Secondary indexes**: Declare whether secondary indexes are required, optional, or unsupported. If optional but not implemented cleanly, the runner must report the feature as unsupported.

Example workload section for a search experiment:

```json
"workload": {
  "type": "search-point-category-scan",
  "lookup": 10000,
  "notes": "Measures existing GetByKey, missing GetByKey, and scan/filter category lookup.",
  "options": {
    "pointExistingQueries": "10000",
    "pointMissingQueries": "10000",
    "scanQueries": "20",
    "cache": "false"
  }
}
```

## What NOT to Do

### ❌ Don't change semantics between targets


All targets in one experiment must execute the **same workload** with the **same semantics**. If one target does `Build()` and another skips it, the comparison is invalid.

### ❌ Don't make conclusions in the runner

Runners write raw facts. Analysis and charts draw conclusions. A runner should never:
- Compute p95/p99/MAD/trimmed mean
- Decide if a result is "good" or "bad"
- Skip writing raw data because "it looks wrong"

### ❌ Don't mix cached and uncached metrics

If one run uses a warm cache and another doesn't, the metrics are not comparable. Use the `warmup` runs to stabilize state before `measured` runs.

### ❌ Don't modify raw results after writing

Raw results are immutable. If you need to fix something, write a new raw file. Analysis stages create separate derived artifacts.
