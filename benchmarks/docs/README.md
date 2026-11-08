# Benchmark reports and reproducibility

**Planned next research:** [direct two-snapshot Polar.DB index evolution study, 2021 → November 2026](EVOLUTION_2021_2026_STUDY_PLAN.md). This is a preregistration outline, **not yet experimental evidence**.

The **completed 8 November independent follow-up** contains 8 selected scenario/row-count series, each with 8 accepted paired process runs (64/64 pairs). The [full statistical report](INDEPENDENT_REPLICATIONS_FINAL_20261108.md) links the [new, immutable archive](../evidence/polar-independent-20261108-8-full.tar.gz) (SHA-256 `eb4fa064131bd1a9b3a4c5c4d6e0959e87ac85c987ea670a7883afe890fabc07`). The earlier [partial report](INDEPENDENT_REPLICATIONS_20261108.md) and its immutable raw archive remain available as historical evidence. These selected same-host exploratory replications do not replace the original 19-scenario 4 November tables or independently replicate results on different hardware.

For the index-equivalence contract used by the current benchmark code, see
[`INDEX_PROFILE_PARITY.md`](INDEX_PROFILE_PARITY.md). The proposed
[`STATE_TRANSITION_PROTOCOL.md`](STATE_TRANSITION_PROTOCOL.md) defines
an empty prepared store, a saved closed database, and a query-ready database,
along with reproducible snapshots and validity gates for
**future** experiments; its measurements have **not** been performed. The article
[`POLAR_DB_ARTICLE_DRAFT.md`](POLAR_DB_ARTICLE_DRAFT.md) reports the completed
November 4, 2026 Linux/WSL2/ext4 series (`20261104T075733840Z`, source SHA
`abbb03cdc34cff1c11e7550494f65b03dad9afd1`) and keeps earlier results
separate. The original [4 November raw archive](../evidence/20261104/README.md)
(19 experiments, SHA-256 published alongside the archive) and the separate
[Stage 4B research snapshot](../evidence/20261107/README.md) are both available
in this repository. Reproduce the manuscript and supplementary values without
rerunning benchmarks from the repository root:

```bash
(cd benchmarks/evidence/20261104 && sha256sum -c SHA256SUMS)
(cd benchmarks/evidence/20261107 && sha256sum -c SHA256SUMS)
mkdir -p benchmarks/results/final-series
tar -xzf benchmarks/evidence/20261104/20261104T075733840Z-abbb03c-ext4.tar.gz \
  -C benchmarks/results/final-series
python3 benchmarks/scripts/check-article-results.py
python3 benchmarks/scripts/check-stage4b-article-results.py
python3 benchmarks/scripts/check-evidence-provenance.py
```

The first checker verifies the 9 main comparative tables and 4 supplementary
tables (204 numeric cells) against the archived CSV/JSON, including paired
reopen-stage fractions. The second independently reconstructs the six rows of
manuscript table 10 from all 48 Stage 4B raw JSON, recalculates the eight
paired log-ratios, their geometric mean, the preregistered block-bootstrap
interval, and the absolute paired difference. The provenance checker verifies both published archive hashes, all 19 main-series
raw JSON digests, the 19 clean coordinator + 38 clean worker source manifests,
and the Stage 4B lock digest and 11 frozen protocol-file hashes. This is a
read-only verification that does not need Git history, Docker, or .NET.

The two manuscript figures have **different sources**: the architecture diagram
is generated from `benchmarks/docs/figures/primary-index-access.dot` with
Graphviz 2.43.0; the reopen-stage plot is one of four SVGs regenerated from
the main-series CSV by `python3 benchmarks/scripts/render-article-figures.py`.
The reader-facing labels use Russian terms with English technical equivalents
in parentheses. The displayed decimal commas do not alter the original CSV
measurements. To check that generated figures still match the committed SVGs,
first extract the original main-series archive as instructed above, then run:

```bash
python3 benchmarks/scripts/render-article-figures.py
git diff --exit-code -- benchmarks/docs/figures/*-5m.svg
dot -Tsvg benchmarks/docs/figures/primary-index-access.dot -o /tmp/primary-index-access.svg
cmp /tmp/primary-index-access.svg benchmarks/docs/figures/primary-index-access.svg
```

Graphviz 2.43.0 was used for the committed architecture diagram; other
Graphviz versions may produce harmless differences in SVG layout or metadata.

**Three distinct reproduction levels:** (1) archive integrity and historical
source/protocol metadata; (2) arithmetic recovery of reported tables and
figures; (3) a full new experimental execution on an independently prepared
system. The published artifacts establish (1) and (2), **not (3)**. The
Stage 4B snapshot omits its multi-gigabyte prepared databases, the exact
kernel page-cache state and the Docker image bytes; a full rerun requires
recreating/sealing source data and satisfying the frozen host protocol. The
main-series archive includes raw measurements but not a physical snapshot of
its original WSL2 environment. A `source-worktree: dirty` note in main-series
`SOURCE.txt` conflicts with 57 clean at-measurement process manifests; its
packaging-time origin remains unexplained and is not silently discarded.

Benchmark executables reference the shared `benchmarks/src/BenchSupport/BenchSupport.csproj`
library. Common benchmark sources are compiled once instead of being linked separately into
every executable.

Each normal benchmark launch is a coordinator. It starts SQLite and Polar.DB in separate
child processes, waits for both workers, validates compatible result shapes and build
settings, and then creates the combined report.

Input benchmark rows are generated lazily, not retained as an O(N) `Row[]` in RAM.
The generator is deterministic (same row IDs, values and order) and can be
re-enumerated independently by each engine. Reopen and mutation result checks
also stream rows through an order-independent checksum instead of materializing
all stored rows. This removes harness-side O(N) row retention, but does not
limit each engine's own index-building memory or OS page cache.

**Measurement boundary:** the current build benchmark's `Load` phase now
includes on-demand row synthesis and its allocations, whereas older commits
created the full dataset before starting `Load`. Compare new `Load`
allocation/GC/timing numbers only with runs from the same implementation.
The isolated `Build`, lookup, and reopen timers do not include generation.
The proposed pre-generated-source protocol in `STATE_TRANSITION_PROTOCOL.md`
remains a separate, not-yet-implemented experiment.

## Prepared databases and query-only lookup pilot (opt-in)

`PreparedLookupRunner` introduces a **separate** `prepare → verify → query → verify`
protocol for lookup benchmark executables. The normal coordinator's build+lookup
measurements are unchanged. Prepared files are stored *outside the repository*,
never under the automatically cleaned `benchmarks/work` directory.

Example in the Linux benchmark container, starting from the repository root:

```bash
B=benchmarks/src/PkIntLookup/bin/Release/net10.0/PkIntLookup.dll
PREP=/work/ram-study/prepared-v1
RESULTS=/work/ram-study/query-results-v1

dotnet build benchmarks/src/PkIntLookup/PkIntLookup.csproj -c Release
for engine in sqlite polar-db; do
  python3 benchmarks/scripts/disk_guard.py --min-free-gib 16 --max-growth-gib 3 \
    --timeout-seconds 300 -- dotnet "$B" --prepared-stage=prepare \
    --prepared-root="$PREP" --benchmark-engine="$engine" --rows=5000000
  dotnet "$B" --prepared-stage=verify \
    --prepared-root="$PREP" --benchmark-engine="$engine" --rows=5000000
done

# Run each engine/phase in a FRESH process, ideally in its own RAM-limited
# container with the prepared-data volume mounted. Use distinct run IDs.
for engine in sqlite polar-db; do
  for phase in cold hot; do
    dotnet "$B" --prepared-stage=query --prepared-root="$PREP" \
      --prepared-results-root="$RESULTS" --benchmark-engine="$engine" \
      --prepared-phase="$phase" --rows=5000000 \
      --benchmark-run-id="pilot-${engine}-${phase}"
  done
  dotnet "$B" --prepared-stage=verify \
    --prepared-root="$PREP" --benchmark-engine="$engine" --rows=5000000
done
```

`prepare` streams deterministic rows, creates indexes and flushes/ closes the
storage, computes the expected per-phase row counts and checksums from the
streamed generator **once**, then seals *permanent* file paths, sizes and SHA-256
hashes, the key-plan batch size and expected results with the source Git SHA.
SQLite's WAL is checkpointed before sealing. `verify` checks
persistent file hashes and rejects any nonempty SQLite WAL; a read-only SQLite
connection can create harmless empty WAL/SHM auxiliary files. `query` checks
file names/sizes without hashing or scanning them beforehand, NEVER calls
`Load`/`Build`, and validates the returned rows/checksums after timing. Re-run
`verify` after every query series to establish that the sealed source is intact.
For Polar.DB the current factory opens read/write file handles, so the
post-query SHA-256 check is especially important. The prepared database is
not a substitute for the still-unimplemented *pre-generated source* experiment
in `STATE_TRANSITION_PROTOCOL.md`.

Each query writes a new JSON file under `RESULTS/<experiment>/<run-id>.json`;
existing outputs are never overwritten. Snapshots before/after file warmup,
open/refresh, key warmup, measured batch, measured latency and close contain
process RSS/private/managed bytes, Linux `/proc/self/io` counters, cgroup
`memory.stat`, `memory.events` and `memory.current`. Timers for opening,
warmup and querying are separate. `memory.peak` is a **container-lifetime**
value, not the peak of a query phase. For a true peak, externally poll the
isolated worker/container. `read_bytes` does not by itself establish physical
SSD traffic through all Docker/WSL layers.

**Important limitations:** `cold` means no explicit file warmup, NOT that OS
page cache is cold (restarting a container does not reset kernel page cache);
`hot` sequentially reads permanent files and warms deterministic query keys,
which does not guarantee every page remains resident. Both phases should be
run in independent containers/processes with controlled RAM and cache state
for a publication study. The checksum oracle and lookup-plan batch size are
precomputed during `prepare`, so `query` generates only its sampled keys and
never scans the full input dataset. Query-only pilot results must not be
merged with the older article series.

Small integration smoke (after Release build):
`python3 benchmarks/scripts/test_prepared_lookup.py`.

### Independent uniform lookup plan v3 (stage 3.1; opt-in)

This fixes a discovered workload limitation **without changing** the published
`LookupPlanner` or original results. The old deterministic arithmetic stride
clusters its first 10,000 primary-key queries in a few regions, and old latency
keys overlap the earlier batch. This new plan samples the entire table by
stable, unbiased SplitMix64 random draws without replacement, seed
`0x6A09E667F3BCC909`. It creates **disjoint** sets of 500 warmup, 10,000 batch,
and 2,000 independent latency keys. The 10,000 batch queries use 100 samples
of 100 each. For 20M rows, all 400 50k-row bins are represented. This is a
uniform key *distribution*, not a guarantee of identical page-cache footprints
between engines.

Reuse the prepared 20M DBs from stage 3. Their original source SHA remains
`0fd84e3da854aa9f536a75861e7838af41955bdc`. An additional, immutable
`*.uniform-v3.json` plan records the **new** runner SHA, original dataset SHA,
SHA-256 of the prepared dataset manifest, key-stream fingerprint, distribution
coverage, and expected rows/checksums. `plan` and `verify` hash the data outside
the timed query; `query` only checks file names/sizes and authenticated plan
metadata, relying on post-run `verify` to detect data changes.

```bash
B=benchmarks/src/PkIntLookup/bin/Release/net10.0/PkIntLookup.dll
DATA=/work/ram-study/stage3-0fd84e3/prepared-20000000
OUT=/work/ram-study/stage3-1-results
SOURCE=0fd84e3da854aa9f536a75861e7838af41955bdc
for engine in sqlite polar-db; do
  dotnet "$B" --prepared-plan=uniform-v3 --prepared-stage=plan \
    --prepared-root="$DATA" --prepared-source-sha="$SOURCE" \
    --benchmark-engine="$engine" --rows=20000000
  # Query each engine in its OWN disposable RAM-limited Docker container.
  dotnet "$B" --prepared-plan=uniform-v3 --prepared-stage=query \
    --prepared-root="$DATA" --prepared-results-root="$OUT" \
    --prepared-source-sha="$SOURCE" --benchmark-engine="$engine" \
    --rows=20000000 --benchmark-run-id="uniform-v3-${engine}-r1"
  dotnet "$B" --prepared-plan=uniform-v3 --prepared-stage=verify \
    --prepared-root="$DATA" --prepared-source-sha="$SOURCE" \
    --benchmark-engine="$engine" --rows=20000000
done
```

During v3 `query`, Linux `mincore` snapshots are captured **immediately before
and after the measured batch** (without reading file contents); phase-specific
`read_bytes`/refault deltas and the independent 2,000 latency samples are
recorded separately. The test rig may optionally use file-scoped
`POSIX_FADV_DONTNEED` before each run, but restarting a container alone does
not evict the shared Linux page cache. No engine-specific RAM superiority or
memory-performance crossover is presumed. The article is not updated by the
study. Test: `python3 benchmarks/scripts/test_uniform_lookup.py`.

### Pinned file-page cache (`uniform-v4`; stage-4 engineering stabilization)

`uniform-v4` is a **new opt-in protocol**, not a repair or reinterpretation of
stage-4 results. It reuses the same sealed prepared databases and the same
12,500 independent primary-key requests but requires a **new, source-SHA-bound**
`<engine>.uniform-v4.json` manifest created by `--prepared-stage=plan`.
Previous `uniform-v3` and `uniform-v32` results and their runner SHAs remain
unchanged. Only new v4 runs use pinned cache.

Linux `mmap(MAP_SHARED)` + `mlock` is applied to **every sealed file before
opening the database**. It forces the file pages into RAM and prevents the
kernel from evicting them until unlocked. The pins are held across the engine
open/refresh, key warmup, measured 10k batch and independent 2k latency
sample. Just before applying the explicit treatment, `mincore` confirms all
files are resident; the selected file is then `munlock`ed and **unmapped**
(outside timing) before `POSIX_FADV_DONTNEED` evicts its pages. All other files
remain locked throughout measurement. A `none` treatment never unlocks any
file until after measurement. `mincore` checks before and after the batch
still apply. Output includes `PinnedCacheProtocol: mlock-v1`,
`PinnedInitiallyBytes` and `PinnedDuringBatchBytes` so accidental unlocks are
observable. The read-only maps never alter prepared data or database bytes.

In Docker, provision a sufficiently large **per-container locked-memory
resource limit** (20M Polar.DB needs roughly 2.3 GB locked file pages):

```bash
docker run --rm --memory=8g --memory-swap=8g \
  --ulimit memlock=3221225472:3221225472 \
  --mount type=volume,source=polar-bench-data,target=/work \
  --workdir /work/Polar.DB mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc 'dotnet ... --prepared-plan=uniform-v4 ...'
```

`--ulimit memlock=3221225472:3221225472` grants 3 GiB of `RLIMIT_MEMLOCK`
without requiring `CAP_IPC_LOCK` on common Linux Docker configurations. If
unsupported by host security policy, `mlock` fails **before** search with an
explicit errno; do not silently fall back to advisory file-cache warming or
weaken the mincore requirements. Pinning consumes host and container memory,
so monitor cgroup memory and terminate/reject attempts that hit `memory.max`
or OOM. `fadvise` is still advisory for selected pages: reject treatments that
fail the specified eviction/residency check. The new protocol guarantees only
the pages successfully pinned, not general cache fairness or equal filesystem
layouts across engines.

For the 20M sealed databases use the same `--prepared-root` and
`--prepared-source-sha` as v32, but first generate a new v4 sidecar using the
**published query runner's exact Git SHA**, then run the corresponding query.
The `--prepared-cache-treatment` values and per-file checks are the same as
v32. Test locally using:

```bash
python3 benchmarks/scripts/test_pinned_cache.py
```

This test includes successful targeted evictions, checksum parity,
`none`-retention, half-file treatment, immutability, isolation from legacy
plans, and a low-`RLIMIT_MEMLOCK` negative case. Do not mix v4 experimental
samples with the already completed stage-4 confirmation ledger or claim
statistical replication merely from engineering smoke tests.

### Controlled per-file cache treatments (stage 3.2; `uniform-v32`)

This separate, opt-in protocol reuses identical `uniform-v3` key streams and
immutable **prepared** databases. Its source-runner SHA is intentionally sealed
in a separate `*.uniform-v32.json` plan so v3 data cannot be silently relabeled.
No source rows or indexes are regenerated. The query stage first performs its
normal file/key warmup, then checks `mincore` residency for *each* permanent file.
Only if all files are >=99.5% resident does it apply the selected Linux
`posix_fadvise(POSIX_FADV_DONTNEED)` treatment **outside the timed section**.
It immediately rechecks and records per-file residency before executing the
same 10,000-query batch and disjoint 2,000-query latency sample.

- Polar.DB: `--prepared-cache-treatment=none|payload|payload-half|hashes|offsets`
- SQLite: `--prepared-cache-treatment=none|data|data-half`

`payload` evicts `f0.bin` (row data), `hashes` evicts `f1.bin` (disk snapshot
of hashes already loaded in a managed `int[]`), and `offsets` evicts `f2.bin`
(offset list read by primary lookup). For `*-half`, the tool evicts the first
half of each up-to-64-MiB file segment, producing distributed ≈50% resident
file pages, rather than one contiguous cold file suffix. Nonselected Polar.DB
index files must remain >=99.5% resident. `data` evicts the whole SQLite
file, whose rowid B-tree integrates indexed lookup and row data; these
physical layouts are not equivalent. The selected condition must satisfy
measured page-residency thresholds or the query is rejected before timing.

Example for prebuilt 20M primary-int data (use separate Docker containers with
8 GiB/no swap; also verify SHA-256 before and after the series):

```bash
B=benchmarks/src/PkIntLookup/bin/Release/net10.0/PkIntLookup.dll
DATA=/work/ram-study/stage3-0fd84e3/prepared-20000000
SRC=0fd84e3da854aa9f536a75861e7838af41955bdc
OUT=/work/ram-study/stage3-2-results
for engine in sqlite polar-db; do
  dotnet "$B" --prepared-plan=uniform-v32 --prepared-stage=plan \
    --prepared-root="$DATA" --prepared-source-sha="$SRC" \
    --benchmark-engine="$engine" --rows=20000000
done
# Controlled Polar.DB payload case (a new run ID is required for each run):
dotnet "$B" --prepared-plan=uniform-v32 --prepared-stage=query \
  --prepared-root="$DATA" --prepared-results-root="$OUT" \
  --prepared-source-sha="$SRC" --benchmark-engine=polar-db \
  --prepared-cache-treatment=payload --rows=20000000 \
  --benchmark-run-id="v32-polar-payload-r1"
```

For scientific inference compare **within an engine** across its controlled
file conditions first. SQLite cannot separately evict index and row pages
using this protocol. Whole-file `read_bytes` is process-level; per-file
attribution comes from the selective treatment and verified per-file residency,
not a kernel per-file physical-I/O counter. Treat code/data-cache differences,
file layout and host-global page-cache competition as confounders. This is not
an automatic claim that a nominal RAM ceiling is binding.

Integration regression: `python3 benchmarks/scripts/test_cache_treatments.py`.

Benchmark working data is disk-bounded. Successful workers delete each completed row-count
case before moving to the next one. Repeated build scenarios also delete warmup and earlier
measured `run-*` directories as soon as their metrics have been captured, retaining only the
current/final run long enough to calculate artifact size. After the coordinator writes combined
raw/manifest/HTML artifacts, the remaining work directory for that experiment is deleted.
Failed active cases are intentionally kept under `benchmarks/work` for diagnostics.

## Disk-safety guard

Both `run-independent-series.py` and `run-new-benchmarks.ps1` invoke
`benchmarks/scripts/disk_guard.py` for builds and benchmark processes.
Before a command starts, the guard requires more than 24 GiB free by default:
16 GiB reserved plus an 8 GiB budget for reduction in free disk space.
It checks available space every 0.5 seconds and stops the entire launched
process group on a threshold breach or after the per-process timeout
(default 900 seconds). A stopped independent series retains the metadata
and completed raw results; failed work directories are not deleted blindly.
The independent runner supports `--min-free-gib` (at least 8) and
`--max-growth-gib` overrides, but smaller values increase risk.
Lookup scenarios have fixed phase plans: `--warmup` and `--samples` are
rejected for lookup projects rather than silently ignored. Successful
independent runs remove only their empty work-root directories.

The guard fails closed if known benchmark output folders are mounted on a
different filesystem from the repository. This is a monitored safety margin,
**not a filesystem quota**: an unbounded write faster than the polling
interval, or termination of the guard itself, could still fill the volume.
For a strict disk cap, run on a dedicated quota-limited filesystem.
Before publication runs, check the actual disk and leave enough reserve for
other processes. Test the guard without large data using
`python -m unittest discover -s benchmarks/scripts -p 'test_disk_guard.py'`.

Generated latest-result artifacts:

- `<experiment>.html` — derived report;
- `<experiment>.manifest.json` — environment, Git commit, build configuration and worker process metadata;
- `<experiment>.raw.json` — combined structured raw result;
- `<experiment>.raw.csv` — one row per measured sample.

Immutable worker and combined artifacts are stored below:

`benchmarks/results/raw/<experiment>/<run-id>/`

A full series can be started from the repository root:

```powershell
pwsh -File .\benchmarks\scripts\run-new-benchmarks.ps1
```

The script assigns one `POLAR_BENCH_RUN_ID` to the whole series. Engine order is
alternated deterministically by default. Set `POLAR_BENCH_ENGINE_ORDER` to
`sqlite-first` or `polar-first` only when a fixed order is required.

For statistically interpretable comparisons, use
[`INDEPENDENT_RUN_STATISTICS.md`](INDEPENDENT_RUN_STATISTICS.md).
It distinguishes within-process timing samples from independent coordinator runs,
prescribes 12 ABBA-counterbalanced repetitions, and gives a paired bootstrap
analysis with checks on commit, machine and build settings. The scripts are
[`run-independent-series.py`](../scripts/run-independent-series.py) and
[`analyze-independent-series.py`](../scripts/analyze-independent-series.py).
The historic benchmark medians do not carry confidence intervals.

For stage-by-stage reopening diagnostics, see
[`REOPEN_STAGE_DECOMPOSITION.md`](REOPEN_STAGE_DECOMPOSITION.md).
A small standalone November 5, 2026 diagnostic check with three measured
repetitions at 50k and 5M rows is documented in
[`REOPEN_DIAGNOSTIC_20261105.md`](REOPEN_DIAGNOSTIC_20261105.md);
its raw CSV/JSON and manifest files are preserved separately under `docs/evidence/`.
Its runtime differs from the November 4 main series and must not be pooled with it.
The `ReopenOnly` raw JSON/CSV exposes per-stage paired timings alongside
constructor-open-only and query-ready totals; a raw-file-handle probe is separate.

For the next primary-offset experiment, see [`PRIMARY_OFFSET_RESIDENCY_PLAN.md`](PRIMARY_OFFSET_RESIDENCY_PLAN.md). It records the planned Windows/Linux comparison of the current FileStream path, `System.IO.RandomAccess`, memory-mapped access and, only if needed, a bounded managed page cache.

The [October 2026 native Guid lookup run](NATIVE_GUID_2026_10_19_RESULTS.md)
contains commit-bound 50k/5M measurements and links to the raw GitHub CI artifacts.
The [controlled external Guid A-B comparison](EXTERNAL_GUID_AB_2026_10_19.md)
compares baseline and allocation-cleanup commits on one runner per row count;
it found no convincing lookup-latency improvement.

The current Polar.DB benchmark row schema stores both Guid fields directly as native
`PTypeEnumeration.guid` values. Earlier benchmark commits represented each Guid as two `long`
fields; those historical raw results remain valid for their recorded commits, but Guid-sensitive
storage-size/build/reopen measurements must be rerun before comparing them with the current schema.

For a new native-Guid experiment use a fresh benchmark run ID and a product commit
containing the native record layout. Do not compare its absolute numbers with the
legacy Guid results as if the physical storage were the same. The typed-storage
`guid-layout.v1` marker is outside the `BenchSupport` record format: these are
separate concerns. The dependency-free [Guid storage microbenchmark](GUID_STORAGE_MICROBENCH.md)
measures only the native-versus-legacy physical encoding on one product commit.

The lifecycle suite also contains focused scenarios used to isolate current storage work:

- `BuildPrimaryIntOnly` — fixed Int32 primary-index build, including Load/Build allocations and GC counts;
- `BuildExternalIndexesOnly` — build and flush of the four typed external indexes over prepared primary storage;
- `TraversalOnly` — full logical row traversal without setup/reopen time in the measured interval;
- `ReopenWithTail` — fixed-Int32 query-ready reopen with 10,000 rows beyond the persisted primary snapshot, isolating tail replay from variable-record recovery scanning.

For lifecycle smoke runs, row counts and operation counts can be overridden:

```powershell
dotnet run --project .\benchmarks\src\AppendOnly\AppendOnly.csproj -- `
  --rows=1000 --warmup=10 --samples=100
```

`--warmup` and `--samples` override lifecycle operation counts. Lookup workloads
use explicit per-phase plans from `BenchmarkDefaults`. Each lookup phase in raw JSON records:

- whether files were explicitly warmed;
- the number of warmup queries;
- measured batch count;
- queries per batch;
- total batch queries;
- latency sample count;
- total measured queries.

Lookup batch and latency key sets have independent expected row counts and checksums.
Both are validated and displayed separately in HTML.

## Publication runs

The shared benchmark library embeds the MSBuild configuration in its assembly metadata.
Every coordinator and worker manifest records:

- `buildConfiguration`;
- whether the build is detected as Debug;
- whether JIT optimizations are disabled;
- whether the run is `publicationReady`;
- explicit `DOTNET_` or `COMPlus_` overrides for tiered compilation, tiered PGO and ReadyToRun.

A run is publication-ready only when the library is an optimized `Release` build. Debug or
otherwise non-optimized runs remain usable for smoke testing, but the console and HTML report
show a prominent `NON-PUBLICATION RUN` warning.

## Metric wording

Lookup HTML reports use explicit names:

- `Batches count` is the number of measured batches.
- `Queries/batch` is the number of lookup requests inside one measured batch.
- `Total queries = Batches count * Queries/batch`.
- `Returned rows` is the total number of materialized rows returned by all lookup requests.
- `Returned rows/query = Returned rows / Total queries`.

Raw CSV uses a generic `value` column plus an explicit `unit` column. Timing rows use
`unit=ms`; managed-allocation rows use `unit=bytes`; GC collection deltas use
`unit=count`. For `lookup-batch-average` rows, `value` is the average milliseconds per
query inside the measured batch and `batch_size` is the actual number of queries used to
produce that sample. For single-query latency rows, `batch_size` is `1`.

Timing tables include `Rows/sec by trimmed` next to trimmed timing columns.
For latency tables this is calculated from trimmed single-query latency and the
average number of rows returned by a latency query.

Lifecycle reports distinguish:

- `open-only` from `query-ready reopen`;
- `volatile mutation` from a batch containing a persistence boundary;
- per-operation raw mutation samples from per-batch durable averages;
- Load/Build timing from process-wide managed allocated-byte and Gen0/1/2 collection deltas.

For build and query scenarios, resource snapshots are captured while the built/query-ready
store is still alive when applicable. This makes retained index memory visible instead of
measuring only the process after the store has been closed.

Green cells mark winners for comparable metrics. Lower is better for timings and
memory sizes. Higher is better for rows/sec and available RAM.
