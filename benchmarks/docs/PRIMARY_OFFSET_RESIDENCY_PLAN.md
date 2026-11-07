# Primary offset residency: measurement plan

Status: the pinned-file confirmation (Stage 4B) is complete. The following access-path and residency-distribution comparison remains **planned, not measured**. Do not implement a managed offset-page cache before its comparison is complete.

## Current state

Current primary-index layout:

- sorted primary hashes are retained in managed RAM as `int[]`;
- static offsets remain in fixed-size Int64 storage;
- lookup finds a hash position in RAM and reads the corresponding offset from storage;
- persisted primary-index format is unchanged.

This gives fast reopen and bounded managed residency, but lookup performance now depends on the operating system file/page cache and the cost of the .NET storage access path.

## Question

Before adding an application-level lazy/paged offset cache, determine how much caching Windows and Linux already provide for the offsets file and whether a managed cache adds enough value to justify duplicated residency and eviction logic.

## Access modes to compare

1. Current `FileStream` / `UniversalSequenceBase.GetByIndex` path.
2. `System.IO.RandomAccess.Read` over the same fixed-size offsets file.
3. `MemoryMappedFile` / mmap-style access with OS-managed lazy residency.
4. Managed bounded paged cache only as a control if modes 1-3 leave a material gap.

Do not change the persisted file format for this experiment.

## Dataset sizes

Use offset files large enough to distinguish full residency from working-set residency:

- 5,000,000 offsets: about 40 MB;
- 50,000,000 offsets: about 400 MB;
- 500,000,000 offsets: about 4 GB, only when the machine and disk budget allow it.

The file contains Int64 offsets and should be generated deterministically.

## Workloads

Measure the same deterministic position sequence for every access mode:

- uniform random positions across the whole file;
- hot working set, initially 1-5% of the file;
- clustered positions representing a hash-collision range / nearby lookups;
- optional sequential scan as a control, not as the primary decision metric.

Run separate cold/first-pass and warm/second-pass phases. Do not combine them into one median.

## Platforms

Required for a publishable comparison:

- native Windows process over NTFS;
- native Ubuntu/Linux process over a native Linux filesystem such as ext4.

The current `rdc-project` container with `/workspace` bind-mounted from Windows `D:` is useful for engineering checks but is not a substitute for either native platform. A Linux-local path such as `/tmp` may be used for exploratory measurements only.

## Metrics

Capture at minimum:

- median, p95 and p99 lookup latency;
- throughput for the same number of offset reads;
- process managed memory;
- working set / RSS and private memory;
- managed allocated bytes and GC counts;
- reopen/open preparation time.

Where practical, also capture OS-level major/minor page faults and physical read I/O. Treat these as platform-specific diagnostics, not as directly interchangeable counters.

## FileStream options

On Windows and Linux compare the default access path with random-access-oriented options separately. Do not assume `FileOptions.RandomAccess` is beneficial: it may reduce readahead that helps clustered accesses.

## Verified post-hoc page accounting (2026-11-07; NOT a new experiment)

Read-only reproduction:

```bash
python3 benchmarks/scripts/analyze_stage4b_offset_io.py
```

The script checks the published 2026-11-07 research snapshot SHA-256, regenerates the *exact* deterministic 12,500-key stream from `UniformLookupPlanner`, checks its SHA-256, then analyzes eight untouched Stage 4B offset-treatment JSON files. This does not run the benchmark. The following numbers must be treated as **exploratory secondary findings derived after the Stage 4B measurements**, not as a preregistered hypothesis:

- The 20-million-row integer-key path uses an identity hash within this key range. Sorting hashes therefore maps searched ID `v` to static position `v - 1`, and `f2.bin` stores its Int64 offset at byte `8 + 8*(v - 1)`.
- Across the exact 10,000 measured lookup IDs, the *minimum* distinct 4 KiB offset pages requested is **8,861**, or **36,294,656 bytes (36.295 MB)**. This count follows from the source-defined access path and sealed key stream, not a fitted occupancy approximation.
- On the eight `f2.bin`-eviction trials, post-batch minus pre-batch `mincore` residency averages **22,570 pages (92.447 MB)**: **2.547×** the minimum page set. Mean process `read_bytes` delta is **93.250 MB**, or **2.569×** that minimum. Six of eight trials have near byte-for-byte correspondence between process read delta and newly resident `f2.bin` pages; the other two differ by ~3 MB. These measurements are consistent with extra pages read into cache, but `read_bytes` is process-wide: **kernel readahead or FileStream buffering has not been identified as the sole cause**.
- During `f2.bin` eviction, average per-query times in the first vs last 1,000-query groups are **250.94 vs 151.60 µs**. The control conditions also change with group position, so attributing the trend solely to page-cache warming is unjustified. No independent key seeds or second host were used in Stage 4B.

**No theorem or new algorithm follows from this secondary finding.** The publishable opportunity is to validate a model of excess page loading, identify its cause, and test a simpler, lower-amplification access path under equal memory and correctness constraints.

## Candidate next experiment (prospective; NOT YET FROZEN OR RUN)

1. **Mechanism and baselines.** Keep the current persistent format and primary-key semantics. Compare the current `FileStream` / `GetByIndex` path against direct `RandomAccess.Read` and `MemoryMappedFile` / mmap access to the same fixed Int64 offset file. Include `FileOptions.RandomAccess` as a separately instrumented option, **not** as an assumed optimization. Distinguish the offset-only micro-operation from the full `UKeyIndex` lookup; a gain in the former does not prove an end-to-end gain.
2. **Interventions.** Keep `f0.bin` and `f1.bin` pinned as in Stage 4B, and control `f2.bin` residency with `mlock`, `munlock` and target eviction. Compare verified 0%, 50% and 100% pinned offset pages, including at 50% a spatially contiguous and a dispersed preselected page set. Confirm the *actual* `mincore` page layout immediately before and after each batch; an advertised RAM percentage alone is not evidence of residency.
3. **Independent variation.** Use at least two precommitted disjoint key-stream seeds and separately launched paired processes; one seed may support model fitting, with the other held out for prediction. Include uniform and locality-skewed (hot-set / Zipf-like) queries as distinct workloads. A second true Linux host or native platform is required before claiming external transfer; another container on the same kernel does not qualify.
4. **Endpoints.** Primary: the between-access-path paired difference in full-lookup mean µs/query with `f2.bin` cold and a fixed budget; secondary: `read_bytes`, `mincore` page gain, independent page faults where reliable, p95/p99, allocation/GC, open time and accuracy. Compute *expected distinct offset pages* from the exact key streams for every trial. Record 100-group trajectories, so any temporal drift is visible rather than averaged away.
5. **Falsification and quality gates.** Candidate predictive model: `latency = baseline + a*(distinct offset pages missing at batch start) + b*(extra pages brought into cache) + error`, with coefficients fitted only on training seeds; compare prediction error on held-out seeds against a constant-latency baseline. If no access-path change reduces read amplification **and** end-to-end latency under matched residency and correctness, report this negative finding rather than adopting a managed cache. Do not pool different seeds or machines as though individual queries were independent experimental units.
6. **Before execution.** Freeze the exact code/data/image SHAs, key streams, pairing order, memory/page-residency criteria, sample size justified by required interval precision, thresholds for a practically meaningful improvement, exclusion rules and analysis in a commit with a recorded SHA-256. A failed prerequisite must remain in the attempt log; exclude neither slow nor inconvenient results. The present section is a **design candidate**, not that immutable registration.

## Decision gate

Prefer the simplest OS-backed mode if it provides bounded residency and lookup latency close to the best measured mode.

Implement a managed paged cache only if measurements show a repeatable material advantage that cannot be obtained with the OS-backed modes. If a managed cache is tested, it must have:

- a hard memory budget;
- fixed-size pages;
- deterministic eviction policy;
- no persisted-format change;
- a fallback to direct storage access;
- explicit hit/miss metrics.

## Related current work

The p1-p4 modernization and preliminary engineering measurements are described in
[`POLAR_DB_ARTICLE_DRAFT.md`](POLAR_DB_ARTICLE_DRAFT.md).

The benchmark protocol and generated-artifact conventions are described in
[`README.md`](README.md).
