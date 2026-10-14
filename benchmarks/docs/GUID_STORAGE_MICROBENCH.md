# Guid physical storage microbenchmark

This small, dependency-free microbenchmark compares two physical encodings in
`UniversalSequenceBase` on the **same product commit and machine**:

- Native `PTypeEnumeration.guid`: 16 bytes per persisted key.
- Legacy `PTypeEnumeration.sstring` with `Guid.ToString("N")`: 1-byte string
  length prefix followed by 32 UTF-8 characters (33 bytes per key).

The payload is a deterministic array of GUIDs derived from ascending `int`
values. Both variants write the same logical data, flush, reopen, fully scan,
and check every decoded GUID against the array. The measured write interval
includes GUID-to-string conversion for legacy storage; the read interval
includes parsing those strings back into GUIDs. Row counts and repeat counts
are bounded, input order alternates each repetition, and each run cleans up
its transient data files. Default: 50,000 rows, five repetitions.

From the repository root, with the .NET 10 SDK available:

```sh
dotnet run -c Release --project benchmarks/src/GuidStorageMicrobench -- 50000 5
```

The program prints CSV rows (`format,rows,repeat,write_ms,read_ms,file_bytes`).
Keep the exact product commit, OS, CPU, SDK, build configuration, warmup and
run environment alongside any reported timing. Use at least the second and
later repetitions for exploratory analysis. This is **not** a substitute for
`BenchSupport` primary/external index benchmarks or historical SQLite vs.
Polar.DB results; it isolates serialization and sequential scan only. No
historical raw results should be overwritten or merged into this sample.
