# Atomic generation publication in Polar.DB

`AtomicGenerationStore` is an opt-in storage-generation publication primitive
(`Polar.DB.SchedulingOptimization`). It does not modify `USequence` file formats
or automatically turn appends into multi-record transactions.

## Workflow

```csharp
var store = new AtomicGenerationStore(generationsRoot);

// Build must create AND fully flush/close all files in candidateDirectory.
using (var candidate = store.Prepare(candidateDirectory =>
{
    BuildDatabaseAndIndexes(candidateDirectory);
}))
{
    // Reopen and verify the candidate before publishing it.
    candidate.Validate(candidateDirectory => VerifyDatabaseAndIndexes(candidateDirectory));
    candidate.Commit();
}

string? activePath = store.Recover(candidateDirectory =>
    VerifyDatabaseAndIndexes(candidateDirectory));
```

There can be only one writer preparing a generation for the same root at a time:
the writer holds an exclusive OS file handle for the duration of preparation,
validation and publication. An open `PendingGeneration` **must be disposed**.
If `Commit` is not called, disposal discards its private directory.

`CURRENT` contains the name of the committed generation directory. It is
published through a flushed temporary file and atomic rename/replacement on
the same filesystem. Recovery selects **only** the directory referenced by
`CURRENT` that also has `_epoch.ready`. Other ready directories might be
orphaned by a crash before publication: they are intentionally ignored.
A corrupt pointer or missing committed directory fails closed. If `CURRENT`
is missing, `Recover` returns null so the caller can opt into a legacy
storage fallback.

## Crash boundaries

| Interrupted after | Recovery |
| --- | --- |
| CreateBuilding / writing data | previous CURRENT (or null) |
| Validate / ready marker | previous CURRENT (or null) |
| Atomic CURRENT publication | new committed generation |
| Corrupt/missing CURRENT target | throws; never guesses another generation |

## Integration constraints

* The candidate must be **independent** of the currently published files; no
  in-place modification of the active generation during preparation.
* The caller must flush and close data/index files before Validate, and Validate
  must check the semantic integrity it needs (including inverse indexes).
* The atomic pointer is the commit boundary. Reader objects must reopen or be
  swapped under the **application's reader gate** to observe a new generation.
* No process may bypass the writer lease to mutate generations concurrently.
  Published generations should be treated as immutable snapshots.
* Directory-entry durability during sudden power loss depends on the filesystem
  and OS. This API does not claim database-wide fsync or transactional safety
  for external files (such as Fog source documents).
* This API does not provide optimistic concurrency, SQL isolation levels,
  multi-store transactions or automated garbage collection of old generations.
* Test failure injection both before and after publication before depending on
  this primitive for any production data. Keep previous generations for rollback.

For Factoria `RRAdapter`, the next integration layer must build and validate a
complete new set of `USequence` data/index files, then publish the generation
and atomically swap readers. A separate protocol is still required if edits to
Fog files must commit in the same logical transaction as the index.
