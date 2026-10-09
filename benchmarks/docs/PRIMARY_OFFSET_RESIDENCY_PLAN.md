# Primary offset residency: measurement plan

Status: planned. Do not implement a managed offset-page cache before this comparison is complete.

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
