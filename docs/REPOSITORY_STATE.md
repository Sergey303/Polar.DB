# REPOSITORY_STATE.md

_Last updated: 2026-04-08_

## 1. Scope of this document

This document fixes the current technical and repository-level state of the `Polar.DB` repository after the accepted changes of the current work cycle and the newer package/documentation update merged into `main`.

It is intended to answer five questions quickly:

1. what is already implemented and should be treated as current behavior;
2. what invariants are now important for further work;
3. what is now part of the public-facing package/documentation baseline;
4. what is still risky or incomplete;
5. what the most logical next steps are.

This is a repository-state document, not a full historical changelog.  
The goal is to describe the **current working model** of the codebase and repository baseline.

---

## 2. Current repository state

The repository centered around `Polar.DB` is currently in a state where the main work of this stage is concentrated around five areas:

- correctness of `Polar` type round-tripping;
- reliability of sequence state / recovery / refresh behavior;
- safer and more ergonomic work with records and indexes;
- stronger solution/tooling/test baseline;
- a newer package/documentation baseline merged into `main`.

In practical terms, the repository is now more consistent in the places where previously there was risk of silent corruption, wrong reconstruction after restart, subtle boundary bugs, or an unclear gap between internal code changes and the public-facing package/documentation state.

---

## 3. Core repository state

### 3.1. `PType <=> object` round-trip is no longer lossy for the fixed cases covered in this stage

The repository now treats `PType -> object -> PType` as something that must preserve meaningful schema information.

The following cases were fixed:

- `PTypeFString(size)` now preserves the fixed string length;
- `PTypeSequence(...)` now preserves the `Growing` flag;
- `PTypeUnion(...)` is now reconstructed correctly.

This means type descriptions can now pass through object form without losing these specific semantic details.

**Current expectation:** for these covered cases, round-trip conversion should preserve the original type meaning rather than degrade to a weaker or partial schema.

---

### 3.2. Repository state for sequence persistence is now based on logical data boundaries, not incidental stream cursor position

The `state` file no longer stores an accidental “current stream position” as if it were the true end of valid data.

The state model now explicitly relies on two service values:

- sequence element count;
- logical end of valid data.

This is an important semantic correction.

**Current expectation:** sequence restore logic should use the real logical boundary of data, not whatever position happened to be left in the stream at the time of saving state.

---

### 3.3. `AppendOffset` is now treated as a logical end-of-sequence concept

One of the main clarifications of the repository is that `AppendOffset` is not just a random mutable position, but the logical point where valid sequence data ends and the next append should start.

This means:

- `AppendOffset` is part of the logical state of the sequence;
- `Stream.Length` and logical sequence end are not blindly treated as identical in all situations;
- recovery/refresh logic must re-establish a correct logical end before continuing writes.

**Important nuance:** protection against writing beyond `AppendOffset` improves safety, but it does **not** automatically make arbitrary in-place overwrite of variable-sized items fully safe.

That remains a separate constraint of the storage model.

---

### 3.4. `UniversalSequenceBase` is now stricter and better aligned with recovery-safe behavior

`UniversalSequenceBase` was strengthened in several directions:

- reading and writing by offsets became more disciplined;
- service operations restore stream position more carefully;
- traversal behavior was improved;
- sorting-related behavior was improved;
- `AppendOffset` semantics were clarified and documented.

The repository therefore moved from a looser “works in normal cases” model toward a stricter “works with explicit logical invariants” model.

**Current expectation:** code that works with sequences should respect the distinction between physical stream state and normalized logical sequence state.

---

### 3.5. Recovery and refresh now distinguish header intent, readable data, garbage tail, and logical end

This is one of the most important current repository properties.

The sequence recovery model now explicitly distinguishes:

- declared count from the header;
- actually readable number of elements;
- garbage tail after the last valid element;
- logical end of valid sequence data.

#### Recovery behavior now assumes

- declared count is an intent, not absolute truth;
- if less data is actually readable, only valid readable items are restored;
- garbage tail after the valid data is treated as garbage and removed from the logical state;
- fixed-size sequences handle mismatches between declared count and actual readable capacity more carefully;
- partially written headers now fail explicitly with `InvalidDataException` instead of being silently normalized away.

#### Refresh behavior now assumes

- fixed-size sequences check consistency between file length and declared count;
- variable-size sequences do not trust `Stream.Length` blindly as logical end;
- logical end is recomputed from what is actually readable;
- after normalization, `AppendOffset` again matches the logical end of valid data.

**Current expectation:** restart/recovery behavior should be substantially more robust in the presence of truncation, stale tail bytes, mismatched header/data, and similar non-ideal file states.

---

### 3.6. `USequence.Build()` now follows the correct persistence order

The repository now assumes that sequence data must be stabilized before index construction is finalized.

The current intended order is:

1. persist/fix the sequence itself;
2. build indexes;
3. persist/fix indexes;
4. save the resulting correct state.

This prevents building index state over a transient or half-finalized view of the underlying data.

**Current expectation:** data, indexes, and state must be persisted in a mutually consistent order.

---

### 3.7. Record access is no longer forced to stay at the raw `object[]` + magic-index level

The repository now includes `RecordAccessor`, which introduces schema-aware access to record fields by name.

This improves ergonomics and reduces positional errors in record handling.

Instead of relying on fragile constructs like:

```csharp
((object[])record)[2]
```

code can work with explicit field names and the known `PTypeRecord` schema.

**Current expectation:** new code that works with records should prefer semantic access by field name where this improves clarity and safety.

---

### 3.8. Index lookup behavior is corrected for first-match boundary cases

`UKeyIndex`/index lookup logic was fixed around repeated-key boundary conditions.

The important behavioral correction is that lookup now follows a binary-search strategy that finds the **first valid matching position** in the covered scenario, rather than an arbitrary duplicate.

This matters because later sequential processing of matching keys depends on starting from the correct boundary.

**Current expectation:** repeated-key index lookups should now behave correctly at the start boundary of the equal-range block.

---

### 3.9. Solution/tooling baseline is modernized and more explicit

At the solution level:

- `global.json` was added to pin SDK selection to .NET 10;
- `Polar.DB` was moved to multi-targeting.

Current target frameworks:

- `netstandard2.0`
- `netstandard2.1`
- `net7.0`
- `net8.0`
- `net10.0`

This is a pragmatic state:

- repository development uses a pinned modern SDK baseline;
- the library remains consumable across a wider runtime surface.

**Current expectation:** contributors should treat SDK choice as repository-controlled, while library consumers can still target broader .NET environments.

---

### 3.10. The repository now has stronger executable regression protection for the changes of this stage

A separate test project was added and key changes are covered by tests.

Covered areas include:

- `RecordAccessor`;
- `PType <=> object` round-trip behavior;
- `UniversalSequenceBase`;
- `UKeyIndex`.

Tested behaviors include:

- fixed string length preservation;
- `Growing` flag preservation for sequences;
- union reconstruction;
- logical-end calculation;
- garbage-tail rejection during recovery;
- explicit failure on partial header;
- correct first-match index lookup;
- record access by field name.

**Current expectation:** these changes are no longer only “knowledge in the author’s head”; they are now partially fixed in executable regression tests.

---


## 4. Repository invariants that should now be treated as important

The following invariants appear to be the most important current ones for further development.

### 4.1. Logical end matters more than incidental cursor position

Any code that persists or restores sequence state should think in terms of **logical valid data boundary**, not “where the stream cursor happened to be left”.

### 4.2. `AppendOffset` should describe append position after normalization

After recovery/refresh/normalization, `AppendOffset` should point to the logical end of valid data.

### 4.3. Header count is informative, but not blindly authoritative

Declared count and real readable data must be cross-checked.

### 4.4. Garbage tail is not data

Bytes after the last valid readable element must not be silently treated as valid sequence content.

### 4.5. Data must be stabilized before indexes/state are finalized

Index/state persistence must reflect finalized data, not an intermediate state.

### 4.6. Raw positional record access is now a lower-level fallback, not the best default

Where possible, schema-aware named access is preferable.

### 4.7. Public package/documentation state should not drift away from repository reality

A newer package/documentation baseline now exists in `main`, so future work should avoid creating drift between:

- what the code actually guarantees;
- what the package exposes;
- what the documentation promises.

---

## 5. What is already in a reasonably good state

Based on the implemented changes and the newer merged baseline, the following areas look materially improved:

- type metadata round-trip correctness for the covered `PType` cases;
- restart/recovery correctness for sequences;
- handling of garbage tails and damaged partial-header situations;
- semantic clarity of `AppendOffset`;
- correctness of duplicate-key index start lookup;
- developer ergonomics for records via `RecordAccessor`;
- SDK/target-framework baseline clarity;
- regression protection for the main fixes of this cycle;
- repository-level package/documentation baseline in `main`.

---

## 6. What is still risky, incomplete, or worth treating as open

This section reflects the most likely remaining pressure points after the current stage.

### 6.1. `stateFileName` remains an architectural pressure point

Even with better state semantics, an external state file can still create operational problems:

- restart friction;
- file locking/process cleanup pain;
- mismatch risk between main data and sidecar state;
- extra coordination burden in environments with crashes or forced termination.

This means the semantic fix for state content is important, but it does not automatically solve the broader architectural question of **where state should live** and **how tightly it should be coupled to the main file**.

### 6.2. Refresh/recovery cost may still matter for large files

The repository is now more correct in how it reconstructs logical end and validates data, but the cost model of refresh/recovery can still matter.

If refresh recalculates append position by wide scans, the repository may still need a more explicit startup/normalization strategy and stronger steady-state invariants.

### 6.3. Variable-size in-place overwrite still deserves care

The current work explicitly clarifies that protection around `AppendOffset` does not magically make arbitrary variable-size rewrite safe.

This area should still be treated conservatively.

### 6.4. Test coverage is stronger, but not equal to full storage-model proof

The repository is in a better state than before, but the test project covers the key repaired behaviors of this stage, not every possible storage corruption or concurrency scenario.

### 6.5. Documentation/package freshness still needs explicit maintenance discipline

Even after the newer merge into `main`, documentation and package state can drift again unless they are maintained deliberately.

The repository will benefit from a simple rule:

- meaningful behavior changes should be reflected in tests;
- externally visible changes should be reflected in package and documentation notes.

---

## 7. Recommended next steps

If the repository continues in the same direction, the most logical next steps are these.

### 7.1. Decide the long-term fate of `stateFileName`

A clear architectural decision is needed between alternatives such as:

- keeping a separate sidecar state file but tightening lifecycle rules;
- using dynamic/safer naming and cleanup conventions;
- deriving more state directly from the main file;
- embedding state into a better-coupled storage structure.

This is not just a minor cleanup item; it affects operational reliability.

### 7.2. Strengthen the steady-state invariant around append position

A useful next direction is to make the runtime invariant more explicit, for example:

- after startup normalization, append state should stay synchronized with actual valid end;
- routine operations should preserve that invariant without requiring expensive re-derivation.

### 7.3. Expand tests around damaged-file and boundary scenarios

Especially valuable:

- more recovery edge cases;
- rewrite/append interaction cases;
- state/index/data divergence scenarios;
- larger duplicate-key index ranges;
- repeated restart/refresh cycles.

### 7.4. Keep public documentation aligned with executable behavior

The repository now has a stronger technical core and a newer documentation baseline.  
The next useful discipline is to make sure that package/docs updates continue to track:

- current invariants;
- supported target frameworks;
- known storage-model constraints;
- practical usage examples.

### 7.5. Continue pushing API clarity over implicit low-level behavior

The addition of `RecordAccessor` is a good sign. The repository benefits when behavior is expressed in semantic APIs rather than through fragile positional conventions or incidental stream state.

---

## 8. Practical summary

At the end of this stage, the repository centered around `Polar.DB` is in a meaningfully better state in the parts that matter most for correctness:

- schema round-trip is less lossy;
- sequence state is based on logical data boundaries;
- recovery is stricter and safer;
- index boundary lookup is more correct;
- record handling is more expressive;
- build order is more consistent;
- tests now anchor the repaired behaviors;
- `main` now also reflects a newer package/documentation baseline.

The main remaining architectural pressure is no longer “basic correctness is obviously broken”, but rather:

- how to make state management operationally cleaner;
- how to reduce refresh/recovery cost while keeping correctness;
- how far to formalize invariants around append/rewrite behavior;
- how to keep public package/documentation state aligned with the real repository behavior.

That is a much healthier next problem to have.
