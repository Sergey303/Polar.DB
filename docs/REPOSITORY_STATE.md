# REPOSITORY_STATE.md

_Last updated: 2026-04-11_

## 1. Scope of this document

This document captures the **current verified working model** of the PolarDB repository, with special attention to the gap between:

- what the repository now does well;
- what is already protected by executable tests;
- what is still under-protected even if the implementation looks improved.

This is intentionally a repository-state document, not a change log and not a marketing summary.

---

## 2. Verified current state

### 2.1. Core storage correctness is materially stronger than before

The repository is in a much healthier state than the earlier baseline in the places where silent data damage would hurt most:

- `UniversalSequenceBase` now treats `AppendOffset` as a logical end-of-sequence concept rather than as an accidental cursor position;
- recovery and refresh now distinguish header intent, readable payload, garbage tail, and logical end;
- partial headers now fail explicitly instead of being silently normalized away;
- `USequence.Build()` now follows a safer persistence order;
- `RecordAccessor` provides schema-aware field access instead of forcing callers to stay on raw magic indexes;
- `UKeyIndex` lookup behavior around duplicate keys was strengthened.

In practical terms, the repository moved from “works in common cases” toward “has explicit invariants in the dangerous cases”.

---

### 2.2. The strongest currently verified test area is `UniversalSequenceBase`

The existing test baseline is already quite meaningful around:

- append behavior;
- header flushing;
- read/write by offset;
- scan / enumeration behavior;
- fixed-size vs variable-size behavior;
- refresh / recovery;
- garbage-tail trimming;
- partial-header failure;
- sort behavior.

This is not superficial coverage. It already protects a large part of the storage primitive contract.

---

### 2.3. Secondary indexing now has basic executable protection, but mostly through integration-style scenarios

The current test suite already covers important integration paths for:

- `UKeyIndex`;
- `SVectorIndex`;
- `UVectorIndex`;
- `UVecIndex`;
- `UIndex` through the `USequence` facade.

That is useful because callers usually interact with indexing through `USequence`, not through low-level internals.

---

## 3. Important repository invariants

These invariants should now be treated as part of the repository’s mental model.

### 3.1. Logical end matters more than current stream cursor position

The durable notion of the sequence tail is `AppendOffset`, not whatever `Stream.Position` happens to be at some moment.

### 3.2. Declared count is informative, but not blindly authoritative

The header is an input to recovery, not absolute truth. Readable payload must still be validated.

### 3.3. Garbage tail is not data

Bytes after the last valid readable element are not part of the logical sequence and must not silently survive normalization.

### 3.4. Data should be stabilized before indexes and state are finalized

The intended persistence order is:

1. persist sequence data;
2. build indexes;
3. persist indexes;
4. save the resulting state.

### 3.5. Raw positional access is now a lower-level fallback

For record-like values, schema-aware named access through `RecordAccessor` is now the safer default.

---

## 4. What is already in a reasonably good state

The following areas look materially improved and already have meaningful regression protection:

- `UniversalSequenceBase` core storage behavior;
- `UKeyIndex` first-match boundary behavior;
- baseline secondary index integration behavior;
- `RecordAccessor` basic named access ergonomics;
- `ByteFlow` basic round-trips for primitive, record, sequence, and union cases;
- core `PType <=> object` regressions for the fixed cases already targeted in this stage.

---

## 5. What is still incomplete or risky

### 5.1. The test project is still not tightly coupled to the current source tree

A major trustworthiness gap remains in the test setup:

- `Testing/Polar.DB.Tests/Polar.DB.Tests.csproj` still references the published NuGet package `Polar.DB` version `2.1.0`;
- it does **not** currently use `ProjectReference` to the local `Polar.DB` project.

That means the repository can show “green tests” while still failing to prove that the **current working tree** is what was actually tested.

This is not a cosmetic concern. It affects the credibility of the whole test baseline.

---

### 5.2. Solution metadata and repository-state documents are ahead of the actual project file in some places

Some documents describe a broader modernized target-framework matrix, but the currently verified `Polar.DB.csproj` still shows only:

- `netstandard2.1`

This means the repository contains a documentation/state drift problem:

- some improvements are described as accepted repository state;
- the checked project file does not yet fully reflect that description.

This is a repository hygiene issue, not just a docs issue, because contributors may make decisions based on the state document.

---

### 5.3. `USequence` as a public lifecycle facade is still under-tested

`UniversalSequenceBase` is much better covered than `USequence`.

The current test suite still leaves too much of the public lifecycle behavior under-protected, especially around:

- `Load()` semantics with `isEmpty`;
- saved state file semantics;
- restart + `RestoreDynamic()`;
- visibility rules for duplicate keys and tombstone-like replacements;
- explicit reindexing through `CorrectOnAppendElement()`.

This matters because external callers usually depend on `USequence` as the operational surface of the library.

---

### 5.4. `RecordAccessor` public API is only partially covered

The current suite verifies the most obvious happy-path operations, but it still under-covers:

- constructor guards;
- schema metadata properties;
- `HasField`;
- `GetFieldType`;
- `CreateRecord()` without values;
- `TryGet<T>()` typed mismatch behavior;
- validation failures for non-array or null values.

This is not catastrophic, but it is exactly the kind of small public-surface drift that later breaks consumers quietly.

---

### 5.5. `ByteFlow` and `PType <=> object` still need deeper edge coverage

The current baseline covers the repaired core cases, but still leaves risk around:

- nested record/sequence combinations;
- sequence-of-record round-trip;
- truncated payload behavior;
- fixed-string shape verification as an executable regression;
- round-trip of sequence `Growing` metadata through object form;
- union shape verification through object round-trip.

These are not “coverage for coverage’s sake”. They are the natural next pressure points after the first repaired cases.

---

### 5.6. Variable-size in-place overwrite is still the most dangerous semantic gap

The code and documentation already acknowledge an important nuance:

- preventing writes beyond `AppendOffset` is not the same thing as making arbitrary variable-size in-place rewrite safe.

This remains the sharpest correctness risk in the storage model.

The repository still needs a clearly fixed contract for:

- same-size in-place rewrite;
- longer in-place rewrite that crosses logical tail;
- failed in-place rewrite rollback behavior;
- mutation safety after a failed write attempt.

Right now this is best treated as an intentionally open risk area until executable tests and implementation agree on the exact contract.

---

## 6. Smart test additions recommended now

The next test step should not be “full coverage of every line”.
It should be **smart contract coverage of the public surface**.

The most useful additions are these five files:

1. `RecordAccessorPublicApiTests.cs`  
   Covers schema metadata, guard clauses, typed reads, and empty-shape creation.

2. `USequenceLifecycleTests.cs`  
   Covers `Load`, state persistence, `RestoreDynamic`, shadowing/tombstones, and `CorrectOnAppendElement`.

3. `SecondaryIndexesLifecycleTests.cs`  
   Covers clear/restart/refresh/collision/replay behavior for the configured secondary index family.

4. `ByteFlowAndPTypeEdgeTests.cs`  
   Covers nested serialization cases and the remaining high-value `PType <=> object` regressions.

5. `UniversalSequenceBaseMutationSafetyTests.cs`  
   Covers the still-dangerous mutation boundary for variable-size in-place rewrite.

These files do not try to prove the whole storage model formally.
They try to protect the public behaviors most likely to regress in realistic maintenance work.

---

## 7. Recommended next actions

### 7.1. Switch the test project from package reference to project reference

This is the most important reliability fix for the test baseline.

Until the tests are wired to the actual source project, “passing tests” remain weaker evidence than they should be.

### 7.2. Add the five smart-coverage test files

This is the best next test investment because it expands contract coverage without exploding into low-value line coverage.

### 7.3. Decide and codify the mutation contract for variable-size in-place rewrite

A clear decision is needed:

- either such rewrites are explicitly unsupported and must fail safely;
- or a narrower safe subset must be defined and protected.

In either case, failed writes must not leave the sequence in a partially mutated state.

### 7.4. Reconcile state documents with actual project metadata

Repository-state documents and checked project files should describe the same reality.

If multi-targeting is intended and accepted, the project file should reflect it.
If not, the state documents should stop claiming it as current behavior.

---

## 8. Practical summary

PolarDB is no longer in the “obviously fragile” stage for its core storage path.

The main verified strengths now are:

- stronger sequence invariants;
- better recovery behavior;
- safer index lookup boundaries;
- more usable record access;
- a meaningful baseline of executable regression tests.

The main remaining pressure points are now more architectural and test-credibility related:

- tests still target the package instead of the live source tree;
- `USequence` is less protected than the low-level storage primitive;
- variable-size in-place rewrite still lacks a fully codified safe contract;
- some state/docs claims still drift from the checked project file.

That is a much healthier class of problems than the repository had before, but they are still important enough to fix before calling the test baseline truly trustworthy.
