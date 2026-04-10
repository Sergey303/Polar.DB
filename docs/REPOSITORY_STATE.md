# REPOSITORY_STATE.md

_Last updated: 2026-04-10_

## 1. Scope of this document

This document fixes the current technical state of the PolarDB repository based on the accepted code and test changes of the current work cycle.

It is intended to answer four questions quickly:

1. what is already implemented and should be treated as current behavior;
2. what invariants are now important for further work;
3. what is still risky or incomplete;
4. what the most logical next steps are.

This is a repository-state document, not a changelog. The goal is to describe the **current working model** of the codebase after the accepted changes.

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

The important practical point is not that every theoretical `PType` composition is now proven, but that the repository no longer relies on implicit assumptions for the specific `PType` shapes used by the current implementation and examples.

### 3.2. `UniversalSequenceBase` now treats logical tail as a first-class invariant

The most important storage-level improvements are concentrated here.

Current tested behavior includes:
- `AppendOffset` is treated as the logical end of data;
- append operations use the logical tail rather than accidental stream position;
- `Clear()` resets the logical state consistently;
- `Flush()` writes the element count header without corrupting position;
- `Refresh()` and constructor recovery normalize state around readable logical data;
- fixed-size overwrite scenarios are explicitly protected by tests;
- failed variable-size overwrite that crosses logical end is treated conservatively and should not be treated as a supported safe scenario.

This is now one of the best-covered parts of the repository.

### 3.3. `USequence.Build()` / restart / traversal semantics are now much better anchored

The repository now has explicit tests for:
- build order and persisted state;
- reopening after build/close;
- lifecycle operations `Clear`, `Flush`, `Close`;
- traversal over only current/original records;
- dynamic restore behavior after reopen;
- lookup consistency across key and secondary indexes.

The practical result is that `USequence` is no longer tested only as a loose integration shell; it now has direct public-contract coverage for its main lifecycle and lookup surface.

### 3.4. Index boundary behavior is materially safer than before

A key repaired area was duplicate-key / repeated-hash lookup behavior in `UKeyIndex`.

Current tests now cover:
- empty index;
- no-match;
- single-match;
- duplicate-key/all-equal hash block scenarios;
- first/last boundary positions;
- dynamic append interaction.

This means the repository now has regression protection around the “find first valid matching position” family of fixes.

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

Where possible, schema-aware named access is preferable.

### 3.6. Binary and text serialization are now covered as explicit repository contracts

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

### 3.7. Serialization layers now have both positive and negative regression coverage

`ByteFlow` has direct regression coverage for:
- primitive branches (`boolean`, `byte`, `character`, `integer`, `longinteger`, `none`);
- `sstring` null serialization behavior;
- record / sequence / union;
- nested structures;
- selected malformed-length cases.

`TextFlow` now has direct regression coverage for:
- positive record / sequence / union round-trips;
- string escaping and parsing;
- primitive textual branches (`boolean`, `character`, `longinteger`, `real`);
- formatted output;
- malformed public parsing inputs through `Deserialize(...)` and `DeserializeSequenseToFlow(...)`;
- nested malformed parsing scenarios inside record / sequence / union payloads;
- instance reader primitives (`Skip`, `ReadBoolean`, `ReadByte`, `ReadChar`, `ReadInt32`, `ReadInt64`, `ReadDouble`, `ReadString`) through dedicated direct tests.

---

## 4. Repository invariants that should now be treated as important

The following invariants appear to be the most important current ones for further development.

### 4.1. Logical tail is more important than physical stream cursor

For sequence storage work, `AppendOffset` should be treated as the source of truth for logical end-of-data behavior.

### 4.2. Recovery/refresh correctness is now a repository-level expectation

Constructor recovery and refresh behavior are not accidental implementation details anymore. They are part of the current technical contract of the repository.

### 4.3. Unsupported overwrite scenarios must fail conservatively

Variable-size in-place overwrite should still be treated conservatively.
The repository should prefer:
- explicit append;
- explicit rebuild;
- explicit rollback or failure;
over ambiguous “it probably works” overwrite behavior.

### 4.4. Index rebuild/state save order matters

Index/state persistence must reflect finalized data, not an intermediate state.

### 4.5. Public ergonomic layers should be tested as public ergonomic layers

`RecordAccessor` and the current `USequence` lifecycle surface are not just helpers anymore.
They now represent intended public usage paths and should continue to be tested directly.

### 4.6. Public parser entry points should be robust against malformed nested payloads

`TextFlow.Deserialize(...)` and `TextFlow.DeserializeSequenseToFlow(...)` should continue to be treated as user-facing contracts.
Malformed nested data should fail clearly rather than produce silent partially-read structures.

---

## 5. What is already in a reasonably good state

Based on the implemented changes, the following areas look materially improved:

- type metadata round-trip correctness for the covered `PType` cases;
- restart/recovery correctness for sequences;
- refresh normalization and append-tail discipline;
- correctness of duplicate-key index start lookup;
- developer ergonomics for records via `RecordAccessor`;
- SDK/target-framework baseline clarity;
- regression protection for the main fixes of this cycle;
- public text parser robustness for both flat and nested malformed-input scenarios.

---

## 6. What is still risky or incomplete

### 6.1. Variable-size overwrite remains a conservative area

Even with improved rollback/error behavior, variable-size overwrite should still not be treated as broadly safe by default.

### 6.2. Full storage corruption proof is still out of scope

The test suite now covers the repaired behaviors that matter most, but it is not a formal proof against every possible corruption/concurrency scenario.

### 6.3. Text parser robustness is much better covered, but still not mathematically exhaustive

`TextFlow` now has both positive and negative coverage at the public entry points, nested malformed-input level, and reader-primitives level.
That said, text parsing still benefits from future additions if new syntax branches or edge cases are introduced.

---

## 7. Recommended next steps

### 7.1. Treat `REPOSITORY_STATE.md` as a living technical map

It is now useful enough to track repository truth, but it should stay conservative and must be updated from code/tests rather than memory.

### 7.2. Add tests only where they close real behavioral gaps

The current repository no longer benefits much from broad speculative test creation.
Future tests should focus on:
- newly introduced public API;
- newly introduced syntax/serialization branches;
- real regression risks discovered during changes.

### 7.3. Continue pushing API clarity over implicit low-level behavior

The addition of `RecordAccessor` is a good sign. The repository benefits when behavior is expressed in semantic APIs rather than through fragile positional conventions or incidental stream state.

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

The main remaining architectural pressure is no longer “basic correctness is obviously broken”, but rather:

- how to make state management operationally cleaner;
- how to reduce refresh/recovery cost while keeping correctness;
- how far to formalize invariants around append/rewrite behavior.

That is a much healthier next problem to have.

---

## Public API test coverage map

### Methodology

This coverage map is based on a conservative audit of the current repository code and current test files in `tests/Polar.DB.Tests`.

Coverage statuses mean:

- **Covered**: the method is exercised by tests and its behavior is directly asserted.
- **Partially covered**: the method is exercised, but only some relevant branches or contracts are asserted.
- **Covered indirectly**: the method is reached through higher-level tests, but there is no direct focused assertion for its own contract.
- **Not found in tests**: no convincing current test usage was found.

This is a behavioral/static audit, not a line-coverage or branch-coverage report.

### Per-class coverage

#### UniversalSequenceBase

| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `UniversalSequenceBase(PType tp_el, Stream media)` | Covered | `UniversalSequenceBaseRecoveryTests`, `UniversalSequenceBaseRefreshTests`, `UniversalSequenceBaseCoreTests` | Constructor recovery/normalization behavior is exercised broadly. |
| `void Clear()` | Covered | `UniversalSequenceBaseCoreTests.Clear_ResetsState_And_SetsAppendOffsetTo8` | Direct reset contract asserted. |
| `void Flush()` | Covered | `UniversalSequenceBaseCoreTests.Flush_WritesHeader_And_PreservesPosition`, `Flush_On_EmptySequence_WritesHeader` | Direct header persistence/asserted. |
| `void Close()` | Covered | `UniversalSequenceBaseCloseTests.Close_Flushes_Header_And_Allows_Reopen_With_Consistent_State`, file-backed reopen tests | Direct close + reopen contract exists. |
| `void Refresh()` | Covered | `UniversalSequenceBaseRefreshTests` | Dedicated valid/invalid refresh behavior covered. |
| `long Count()` | Covered | multiple `UniversalSequenceBase*Tests` | Asserted broadly. |
| `long ElementOffset(long ind)` | Covered | `UniversalSequenceBaseCoreTests.ElementOffset_For_Fixed_Size_Type_CalculatesCorrectly` | Direct fixed-size offset contract covered. |
| `long ElementOffset()` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseOverwriteTests` | Direct append-offset alias usage covered. |
| `long AppendElement(object v)` | Covered | `UniversalSequenceBaseCoreTests`, recovery/overwrite tests | Offset/count/tail behavior asserted. |
| `object GetElement()` | Covered | `UniversalSequenceBaseLowLevelPrimitiveTests.GetElement_From_Current_Stream_Position_Reads_Current_Record` | Direct current-position read contract covered. |
| `object GetElement(long off)` | Covered | `UniversalSequenceBaseCoreTests`, refresh/recovery tests | Direct offset read covered. |
| `object GetTypedElement(PType tp, long off)` | Covered | `UniversalSequenceBaseCoreTests.GetTypedElement_ByOffset_ReturnsValue` | Direct typed read covered. |
| `object GetByIndex(long index)` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseSortingTests` | Direct index-based read covered. |
| `long SetElement(object v)` | Covered | `UniversalSequenceBaseLowLevelPrimitiveTests.SetElement_At_Current_Stream_Position_Writes_And_Returns_Offset` | Direct current-position write contract covered. |
| `void SetElement(object v, long off)` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseOverwriteTests` | In-place overwrite and tail boundary behavior covered. |
| `void SetTypedElement(PType tp, object v, long off)` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseOverwriteTests` | Typed overwrite covered. |
| `IEnumerable<object> ElementValues()` | Covered | `UniversalSequenceBaseCoreTests` | Direct enumeration contract covered. |
| `IEnumerable<object> ElementValues(long offset, long number)` | Covered | `UniversalSequenceBaseCoreTests.ElementValues_Range_Returns_Subset` | Range enumeration covered. |
| `void Scan(Func<long, object, bool> handler)` | Covered | `UniversalSequenceBaseCoreTests.Scan_RestoresPosition_AfterEarlyStop` | Direct scan behavior covered. |
| `IEnumerable<Tuple<long, object>> ElementOffsetValuePairs()` | Covered | `UniversalSequenceBaseCoreTests.ElementOffsetValuePairs_Returns_All` | Direct pair enumeration covered. |
| `IEnumerable<Tuple<long, object>> ElementOffsetValuePairs(long offset, long number)` | Covered | `UniversalSequenceBaseCoreTests.AppendElement_AfterElementOffsetValuePairsEnumeration_RestoresPosition` | Range pair enumeration covered. |
| `void Sort32(Func<object, int> keyFun)` | Covered | `UniversalSequenceBaseSortingTests` | Direct sorting coverage exists. |
| `void Sort64(Func<object, long> keyFun)` | Covered | `UniversalSequenceBaseSortingTests` | Direct sorting coverage exists. |

#### USequence

| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `USequence(PType tp_el, string? stateFileName, Func<Stream> streamGen, Func<object, bool> isEmpty, Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool optimise = true)` | Covered | `USequenceTests`, `USequenceBuildOrderTests`, `USequenceTraversalTests`, `USequenceLifecycleTests` | Constructor exercised directly by integration tests. |
| `void RestoreDynamic()` | Covered | `USequenceTests.RestoreDynamic_Indexes_Records_Appended_After_Last_Saved_State` | Dedicated dynamic restore contract covered. |
| `void Clear()` | Covered | `USequenceLifecycleTests.Clear_Resets_Visible_Sequence_State_And_Keeps_Object_Reusable` | Direct lifecycle reset covered. |
| `void Flush()` | Covered | `USequenceLifecycleTests.Flush_Persists_Current_State_File_Without_Build` | Direct lifecycle flush covered. |
| `void Close()` | Covered | `USequenceLifecycleTests.Close_Flushes_State_And_Reopen_Remains_Consistent` | Direct lifecycle close covered. |
| `void Refresh()` | Covered | `USequenceBuildOrderTests`, reopen/traversal tests | Reopen/refresh consistency covered. |
| `void Load(IEnumerable<object> flow)` | Covered | `USequenceTests.Load_Skips_Empty_Records_And_Writes_State_File` | Direct load behavior covered. |
| `IEnumerable<object> ElementValues()` | Covered | `USequenceTests`, `USequenceTraversalTests`, `USequenceLifecycleTests` | Empty/superseded filtering covered. |
| `void Scan(Func<long, object, bool> handler)` | Covered | `USequenceTests`, `USequenceTraversalTests` | Direct filtered traversal covered. |
| `long AppendElement(object element)` | Covered | `USequenceTests`, `USequenceBuildOrderTests`, `USequenceLifecycleTests` | Direct append behavior covered. |
| `void CorrectOnAppendElement(long off)` | Covered | `USequenceTests.CorrectOnAppendElement_Indexes_Record_Added_Directly_To_Base_Sequence` | Direct helper contract covered. |
| `object GetByKey(IComparable keysample)` | Covered | `USequenceTests`, `USequenceBuildOrderTests` | Primary-key lookup covered. |
| `IEnumerable<object> GetAllByValue(int nom, IComparable value, Func<object, IEnumerable<IComparable>> keysFunc, bool ignorecase = false)` | Covered | `USequenceBuildOrderTests` | Secondary lookup behavior covered. |
| `IEnumerable<object> GetAllBySample(int nom, object osample)` | Covered | `USequenceBuildOrderTests` | Sample-based lookup covered. |
| `IEnumerable<object> GetAllByLike(int nom, object sample)` | Covered | `USequenceLikeTests.GetAllByLike_Returns_Expected_Matches_Excludes_Superseded_And_Empty_Records` | Like-query integration test exists. |
| `void Build()` | Covered | `USequenceTests`, `USequenceBuildOrderTests` | Build/order/reopen/index consistency covered. |

#### RecordAccessor

| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `RecordAccessor(PTypeRecord recordType)` | Covered | `RecordAccessorTests` | Constructor exercised broadly. |
| `PTypeRecord RecordType` | Covered | `RecordAccessorPropertyTests.RecordType_Returns_Original_Schema_Instance` | Direct property contract covered. |
| `int FieldCount` | Covered | `RecordAccessorPropertyTests.FieldCount_Returns_Number_Of_Declared_Fields` | Direct property contract covered. |
| `IEnumerable<string> FieldNames` | Covered | `RecordAccessorTests.FieldNames_Preserve_Schema_Order` | Direct order contract covered. |
| `bool HasField(string fieldName)` | Covered | `RecordAccessorTests.HasField_Returns_True_For_Existing_Field_And_False_For_Missing_Field`, `HasField_Throws_ArgumentNullException_For_Null` | Direct positive/negative coverage exists. |
| `int GetIndex(string fieldName)` | Covered | `RecordAccessorTests.GetIndex_Returns_Stable_Field_Position`, `GetIndex_Throws_ArgumentNullException_For_Null` | Direct lookup + null coverage exists. |
| `PType GetFieldType(string fieldName)` | Covered | `RecordAccessorTests.GetFieldType_Returns_Declared_Field_Type` | Direct declared-type lookup covered. |
| `object[] CreateRecord()` | Covered | `RecordAccessorTests.CreateRecord_Creates_Array_With_Correct_Length` | Parameterless create contract covered. |
| `object[] CreateRecord(params object[] values)` | Covered | multiple `RecordAccessorTests` | Correct/null/wrong-count behavior covered. |
| `void ValidateShape(object record)` | Covered | `RecordAccessorTests.ValidateShape_Throws_On_Invalid_Field_Count`, `ValidateShape_Throws_On_NonObjectArray` | Direct shape-validation coverage exists. |
| `object Get(object record, string fieldName)` | Covered | `RecordAccessorTests.Get_And_Set_ByFieldName_Work` | Direct getter coverage exists. |
| `T Get<T>(object record, string fieldName)` | Covered | multiple `RecordAccessorTests` | Typed getter covered. |
| `void Set(object record, string fieldName, object value)` | Covered | multiple `RecordAccessorTests` | Direct setter coverage exists. |
| `bool TryGet(object record, string fieldName, out object value)` | Covered | `RecordAccessorTests.TryGet_Returns_False_For_Missing_Field` | Direct missing-field behavior covered. |
| `bool TryGet<T>(object record, string fieldName, out T value)` | Covered | `RecordAccessorTests.TryGet_Typed_Returns_True_For_Correct_Type`, `TryGet_Typed_Returns_False_For_Wrong_Type` | Typed success/mismatch coverage exists. |

#### ByteFlow

| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `static void Serialize(BinaryWriter bw, object v, PType tp)` | Covered | `ByteFlowTests` | Primitive, record, sequence, union, nested, and selected negative cases covered. |
| `static object Deserialize(BinaryReader br, PType tp)` | Covered | `ByteFlowTests` | Primitive, record, sequence, union, nested, and selected negative cases covered. |

#### TextFlow

| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `static void Serialize(TextWriter tw, object v, PType tp)` | Covered | `TextFlowTests`, `TextFlowPrimitiveTests` | Positive record/union/primitive textual branches covered. |
| `static void SerializeFormatted(TextWriter tw, object v, PType tp, int level)` | Covered | `TextFlowTests.SerializeFormatted_ForNestedRecord_AddsLineBreaks` | Direct formatting contract covered. |
| `static void SerializeFlowToSequense(TextWriter tw, IEnumerable<object> flow, PType tp)` | Covered | `TextFlowTests.SerializeFlowToSequense_And_DeserializeSequenseToFlow_RoundTrip` | Direct round-trip exists. |
| `static void SerializeFlowToSequenseFormatted(TextWriter tw, IEnumerable<object> flow, PType tp, int level)` | Covered | `TextFlowPrimitiveTests.SerializeFlowToSequenseFormatted_Produces_Readable_Multiline_Output` | Direct formatted output test exists. |
| `static object Deserialize(TextReader tr, PType tp)` | Covered | `TextFlowTests`, `TextFlowNegativeTests`, `TextFlowMalformedNestedTests` | Positive, malformed-flat, and malformed-nested public parsing are covered. |
| `static IEnumerable<object> DeserializeSequenseToFlow(TextReader tr, PType tp)` | Covered | `TextFlowTests`, `TextFlowNegativeTests`, `TextFlowMalformedNestedTests` | Positive, malformed-flat, and malformed-nested public parsing are covered. |
| `void Skip()` | Covered | `TextFlowReaderPrimitiveTests` | Direct reader-level behavior covered. |
| `bool ReadBoolean()` | Covered | `TextFlowReaderPrimitiveTests` | Direct positive reader contract covered. Strict invalid-token rejection is not asserted. |
| `byte ReadByte()` | Covered | `TextFlowReaderPrimitiveTests`, `TextFlowReaderNegativeTests` | Direct positive and invalid-token reader coverage exists. |
| `char ReadChar()` | Covered | `TextFlowReaderPrimitiveTests` | Direct reader contract covered. |
| `int ReadInt32()` | Covered | `TextFlowReaderPrimitiveTests`, `TextFlowReaderNegativeTests` | Direct positive and invalid-token reader coverage exists. |
| `long ReadInt64()` | Covered | `TextFlowReaderPrimitiveTests`, `TextFlowReaderNegativeTests` | Direct positive and invalid-token reader coverage exists. |
| `double ReadDouble()` | Covered | `TextFlowReaderPrimitiveTests`, `TextFlowReaderNegativeTests` | Direct positive and invalid-token reader coverage exists. |
| `string ReadString()` | Covered | `TextFlowTests.Deserialize_String_Parses_Escape_Sequences`, `TextFlowReaderPrimitiveTests`, `TextFlowReaderNegativeTests` | Direct positive and malformed reader coverage exists. |

### Strongly covered areas

- `UniversalSequenceBase` core behavior: append, overwrite, refresh/recovery, traversal helpers, sorting, and close/reopen.
- `USequence` build/traversal/index usage, dynamic restore, and lifecycle coverage (`Clear`, `Flush`, `Close`).
- `RecordAccessor` main ergonomic API plus helper/tolerant methods and properties.
- `ByteFlow` primitive and composite binary serialization round-trips.
- `TextFlow` positive serialization/deserialization flows, malformed public parsing, nested malformed parsing, formatted output, and direct reader-primitive contracts.

### Partially covered or missing areas

- Full parser-proof completeness for every conceivable `TextFlow` malformed syntax branch is still not mathematically exhaustive.
- Some lower-priority edge cases may still be worth adding only if future code changes make them relevant.
- The current suite is very strong for the repaired behaviors, but it is still not a formal proof against every storage corruption or concurrency scenario.

### Honest repository-level summary

The repository now has strong regression coverage for the repaired behaviors that matter most: storage recovery/refresh, append-offset discipline, overwrite boundaries, key-index boundary behavior, record ergonomics, build order, lifecycle stability, binary/text serialization paths, malformed public parsing, malformed nested parsing, and direct text-reader primitive behavior.

At this point, the remaining work is mostly polish and future-change tracking rather than large missing coverage holes.
