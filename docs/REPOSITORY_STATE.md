# REPOSITORY_STATE.md

_Last updated: 2026-04-10_

## 1. Scope of this document

This document fixes the current technical state of the PolarDB repository based on the accepted code and test changes of the current work cycle.

It is intended to answer four questions quickly:

1. what is already implemented and should be treated as current behavior;
2. what invariants are now important for further work;
3. what is still risky or incomplete;
4. what the most logical next steps are.

This is a repository-state document, not a changelog. The goal is to describe the current working model of the codebase after the accepted changes.

---

## 2. Current repository state

PolarDB is currently in a state where the main work of this stage was concentrated around four areas:

- correctness of `Polar` type round-tripping;
- reliability of sequence state / recovery / refresh behavior;
- safer and more ergonomic work with records and indexes;
- stronger solution/tooling/test baseline.

In practical terms, the repository is now noticeably more consistent in the places where previously there was risk of silent corruption, wrong reconstruction after restart, or subtle boundary bugs.

---

## 3. Core technical state

### 3.1. `PType <=> object` round-trip is no longer lossy for the fixed cases covered in this stage

The repository now has explicit regression tests around the `PType <=> object` conversion surface for the currently used and important type branches.

Covered branches include:
- fixed string;
- sequences with `Growing`;
- union;
- record;
- nested record/sequence cases used by current code and tests.

### 3.2. `UniversalSequenceBase` now treats logical tail as a first-class invariant

Current tested behavior includes:
- `AppendOffset` is treated as the logical end of data;
- append operations use the logical tail rather than accidental stream position;
- `Clear()` resets the logical state consistently;
- `Flush()` writes the element count header without corrupting position;
- `Refresh()` and constructor recovery normalize state around readable logical data;
- fixed-size overwrite scenarios are explicitly protected by tests;
- failed variable-size overwrite that crosses logical end is treated conservatively.

### 3.3. `USequence.Build()` / restart / traversal semantics are now much better anchored

The repository now has explicit tests for:
- build order and persisted state;
- reopening after build/close;
- lifecycle operations `Clear`, `Flush`, `Close`;
- repeated-cycle/idempotency scenarios around `Flush`, `Refresh`, `Close`, append-after-reopen, and build-after-reopen;
- traversal over only current/original records;
- dynamic restore behavior after reopen;
- lookup consistency across key and secondary indexes.

### 3.4. Index boundary behavior is materially safer than before

A key repaired area was duplicate-key / repeated-hash lookup behavior in `UKeyIndex`.

Current tests now cover:
- empty index;
- no-match;
- single-match;
- duplicate-key/all-equal hash block scenarios;
- first/last boundary positions;
- dynamic append interaction;
- repeated reopen/build cycles for primary-key and secondary-index consistency;
- large same-hash ranges with first/middle/last lookup checks;
- large duplicate-key blocks;
- append-after-build on long collision blocks without full rebuild.

This gives the repository regression protection not only for toy examples, but also for realistic long collision/duplicate ranges.

### 3.5. `RecordAccessor` is now the preferred record ergonomics layer and is directly tested as such

The repository now has direct tests not only for `Get/Set/CreateRecord`, but also for:
- `HasField`;
- `GetIndex`;
- `GetFieldType`;
- `TryGet`;
- `TryGet<T>`;
- property-level surface such as `RecordType` and `FieldCount`;
- invalid field names;
- duplicate field names;
- invalid shape cases;
- null/wrong-count argument cases.

### 3.6. Binary and text serialization are now covered as explicit repository contracts

Current target frameworks:
- `netstandard2.0`
- `netstandard2.1`
- `net7.0`
- `net8.0`
- `net10.0`

`ByteFlow` has direct regression coverage for primitive and composite branches.
`TextFlow` has direct regression coverage for positive round-trips, malformed public parsing, nested malformed parsing, formatted output, and reader primitives.

---

## 4. Repository invariants that should now be treated as important

### 4.1. Logical tail is more important than physical stream cursor

For sequence storage work, `AppendOffset` should be treated as the source of truth for logical end-of-data behavior.

### 4.2. Recovery/refresh correctness is now a repository-level expectation

Constructor recovery and refresh behavior are part of the current technical contract of the repository.

### 4.3. Unsupported overwrite scenarios must fail conservatively

Variable-size in-place overwrite should still be treated conservatively.

### 4.4. Index rebuild/state save order matters

Index/state persistence must reflect finalized data, not an intermediate state.

### 4.5. Public ergonomic layers should be tested as public ergonomic layers

`RecordAccessor` and the current `USequence` lifecycle surface are intended public usage paths and should continue to be tested directly.

### 4.6. Public parser entry points should be robust against malformed nested payloads

`TextFlow.Deserialize(...)` and `TextFlow.DeserializeSequenseToFlow(...)` should fail clearly on malformed nested data.

### 4.7. Repeated persistence cycles must remain stable

A single successful `Flush`, `Refresh`, `Close`, `Build`, or reopen is not enough.

### 4.8. Index fixes must survive realistic large collision/duplicate blocks

Small examples are not enough for index correctness.
The repository now treats long same-hash and long duplicate-key ranges as meaningful regression scenarios.

---

## 5. What is already in a reasonably good state

The following areas look materially improved:

- type metadata round-trip correctness for the covered `PType` cases;
- restart/recovery correctness for sequences;
- refresh normalization and append-tail discipline;
- correctness of duplicate-key index start lookup;
- developer ergonomics for records via `RecordAccessor`;
- SDK/target-framework baseline clarity;
- regression protection for the main fixes of this cycle;
- public text parser robustness for both flat and nested malformed-input scenarios;
- repeated-cycle stability for `USequence` persistence/lifecycle operations;
- index behavior on larger collision/duplicate blocks.

---

## 6. What is still risky or incomplete

### 6.1. Variable-size overwrite remains a conservative area

Even with improved rollback/error behavior, variable-size overwrite should still not be treated as broadly safe by default.

### 6.2. Full storage corruption proof is still out of scope

The test suite now covers the repaired behaviors that matter most, but it is not a formal proof against every possible corruption/concurrency scenario.

### 6.3. Text parser robustness is much better covered, but still not mathematically exhaustive

`TextFlow` now has both positive and negative coverage at the public entry points, nested malformed-input level, and reader-primitives level.

### 6.4. Secondary-index parameter branches are still lower-priority polish

Core secondary-index behavior is covered, but some parameter-specific combinations remain more nice-to-have than must-have until a real bug points to them.

---

## 7. Recommended next steps

### 7.1. Treat `REPOSITORY_STATE.md` as a living technical map

It should stay conservative and must be updated from code/tests rather than memory.

### 7.2. Add tests only where they close real behavioral gaps

Future tests should focus on:
- newly introduced public API;
- newly introduced syntax/serialization branches;
- real regression risks discovered during changes.

---

## 8. Practical summary

At the end of this stage, PolarDB is in a meaningfully better state in the parts that matter most for correctness:

- schema round-trip is less lossy;
- sequence state is based on logical data boundaries;
- recovery is stricter and safer;
- index boundary lookup is more correct;
- record handling is more expressive;
- build order is more consistent;
- tests now anchor the repaired behaviors.

The main remaining architectural pressure is no longer basic correctness, but how to keep correctness while evolving behavior and performance.

---

## Public API test coverage map

### Methodology

This coverage map is based on a conservative audit of the current repository code and current test files in `tests/Polar.DB.Tests`.

Coverage statuses mean:
- Covered
- Partially covered
- Covered indirectly
- Not found in tests

This is a behavioral/static audit, not a line-coverage report.

### Per-class coverage

#### UniversalSequenceBase
Core public lifecycle, read/write, enumeration, sorting, refresh/recovery, and close/reopen behavior are directly covered.

#### USequence
Public lifecycle, load/build/refresh/append, traversal, repeated-cycle stability, and main lookup surface are directly covered.

#### RecordAccessor
Constructor, properties, helper/tolerant methods, create/get/set/validate/try-get surface are directly covered.

#### ByteFlow
Public serialize/deserialize surface is directly covered across primitive and composite branches.

#### TextFlow
Public serialize/deserialize surface, malformed parsing, nested malformed parsing, and reader-primitive surface are directly covered.

### Strongly covered areas

- `UniversalSequenceBase` core behavior: append, overwrite, refresh/recovery, traversal helpers, sorting, and close/reopen.
- `USequence` build/traversal/index usage, dynamic restore, lifecycle coverage, and repeated-cycle/idempotency coverage.
- `RecordAccessor` main ergonomic API plus helper/tolerant methods and properties.
- `ByteFlow` primitive and composite binary serialization round-trips.
- `TextFlow` positive serialization/deserialization flows, malformed public parsing, nested malformed parsing, formatted output, and direct reader-primitive contracts.
- internal index behavior on large collision and duplicate ranges.

### Partially covered or missing areas

- Full parser-proof completeness for every conceivable `TextFlow` malformed syntax branch is still not mathematically exhaustive.
- Some lower-priority edge cases may still be worth adding only if future code changes make them relevant.
- The current suite is very strong for the repaired behaviors, but it is still not a formal proof against every storage corruption or concurrency scenario.

### Honest repository-level summary

The repository now has strong regression coverage for the repaired behaviors that matter most: storage recovery/refresh, append-offset discipline, overwrite boundaries, key-index boundary behavior, large duplicate/hash-collision behavior, record ergonomics, build order, lifecycle stability, binary/text serialization paths, malformed public parsing, malformed nested parsing, direct text-reader primitive behavior, and repeated-cycle persistence stability.

At this point, the remaining work is mostly polish and future-change tracking rather than large missing coverage holes.
