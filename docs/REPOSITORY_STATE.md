# REPOSITORY_STATE.md

_Last updated: 2026-04-10_

## 1. Scope of this document

This document fixes the current technical state of the PolarDB repository based on the changes implemented in the current work cycle.

It is intended to answer four questions quickly:

1. what is already implemented and should be treated as current behavior;
2. what invariants are now important for further work;
3. what is still risky or incomplete;
4. what the most logical next steps are.

This is a repository-state document, not a change log. The goal is to describe the **current working model** of the codebase after the accepted changes.

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

---

## 5. What is already in a reasonably good state

Based on the implemented changes, the following areas look materially improved:

- type metadata round-trip correctness for the covered `PType` cases;
- restart/recovery correctness for sequences;
- handling of garbage tails and damaged partial-header situations;
- semantic clarity of `AppendOffset`;
- correctness of duplicate-key index start lookup;
- developer ergonomics for records via `RecordAccessor`;
- SDK/target-framework baseline clarity;
- regression protection for the main fixes of this cycle.

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

### 7.4. Continue pushing API clarity over implicit low-level behavior

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
| `UniversalSequenceBase(PType tp_el, Stream media)` | Covered | `UniversalSequenceBaseRecoveryTests`, `UniversalSequenceBaseRefreshTests`, `UniversalSequenceBaseCoreTests` | Constructor recovery/normalization behavior is exercised broadly through helpers and dedicated recovery tests. |
| `void Clear()` | Covered | `UniversalSequenceBaseCoreTests.Clear_ResetsState_And_SetsAppendOffsetTo8` | Directly asserts clearing, header reset, and append offset reset. |
| `void Flush()` | Covered | `UniversalSequenceBaseCoreTests.Flush_WritesHeader_And_PreservesPosition`, `Flush_On_EmptySequence_WritesHeader` | Directly asserts header persistence and position preservation. |
| `void Close()` | Covered indirectly | file-backed recovery/reopen tests | Close is used in reopen/persistence scenarios, but no dedicated direct contract test is currently tracked here. |
| `void Refresh()` | Covered | `UniversalSequenceBaseRefreshTests` | Dedicated tests cover valid refresh, normalization boundaries, and corruption/exception cases. |
| `long Count()` | Covered | multiple `UniversalSequenceBase*Tests` | Count is directly asserted across core, refresh, recovery, overwrite, and sorting scenarios. |
| `long ElementOffset(long ind)` | Covered | `UniversalSequenceBaseCoreTests.ElementOffset_For_Fixed_Size_Type_CalculatesCorrectly` | Direct fixed-size offset calculation test. |
| `long ElementOffset()` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseOverwriteTests` | Direct append-offset/legacy-alias usage is asserted. |
| `long AppendElement(object v)` | Covered | `UniversalSequenceBaseCoreTests`, recovery/overwrite tests | Extensively asserted for offset, count, append tail, and recovery interactions. |
| `object GetElement()` | Covered | `UniversalSequenceBaseLowLevelPrimitiveTests.GetElement_From_Current_Stream_Position_Reads_Current_Record` | Direct current-position low-level read contract is now tested. |
| `object GetElement(long off)` | Covered | `UniversalSequenceBaseCoreTests.GetElement_ByOffset_ReturnsValue_ForFixedSize`, refresh/recovery tests | Direct offset-based reading is asserted. |
| `object GetTypedElement(PType tp, long off)` | Covered | `UniversalSequenceBaseCoreTests.GetTypedElement_ByOffset_ReturnsValue` | Explicit typed read is directly asserted. |
| `object GetByIndex(long index)` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseSortingTests` | Direct fixed-size index-based read is covered. |
| `long SetElement(object v)` | Covered | `UniversalSequenceBaseLowLevelPrimitiveTests.SetElement_At_Current_Stream_Position_Writes_And_Returns_Offset` | Direct current-position low-level write contract is now tested. |
| `void SetElement(object v, long off)` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseOverwriteTests` | In-place overwrite, tail boundaries, and rollback-related behavior are directly asserted. |
| `void SetTypedElement(PType tp, object v, long off)` | Covered | `UniversalSequenceBaseCoreTests`, `UniversalSequenceBaseOverwriteTests` | Typed overwrite path is directly asserted. |
| `IEnumerable<object> ElementValues()` | Covered | `UniversalSequenceBaseCoreTests` | Direct enumeration and position-restoration behavior are asserted. |
| `IEnumerable<object> ElementValues(long offset, long number)` | Covered | `UniversalSequenceBaseCoreTests.ElementValues_Range_Returns_Subset` | Direct range-enumeration test exists. |
| `void Scan(Func<long, object, bool> handler)` | Covered | `UniversalSequenceBaseCoreTests.Scan_RestoresPosition_AfterEarlyStop` | Direct scan behavior with early stop is asserted. |
| `IEnumerable<Tuple<long, object>> ElementOffsetValuePairs()` | Covered | `UniversalSequenceBaseCoreTests.ElementOffsetValuePairs_Returns_All` | Direct pair enumeration test exists. |
| `IEnumerable<Tuple<long, object>> ElementOffsetValuePairs(long offset, long number)` | Covered | `UniversalSequenceBaseCoreTests.AppendElement_AfterElementOffsetValuePairsEnumeration_RestoresPosition` | Range pair enumeration is exercised and position restoration is asserted. |
| `void Sort32(Func<object, int> keyFun)` | Covered | `UniversalSequenceBaseSortingTests` | Sorting by 32-bit keys is directly regression-tested. |
| `void Sort64(Func<object, long> keyFun)` | Covered | `UniversalSequenceBaseSortingTests` | Sorting by 64-bit keys is directly regression-tested. |

#### USequence
| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `USequence(PType tp_el, string? stateFileName, Func<Stream> streamGen, Func<object, bool> isEmpty, Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool optimise = true)` | Covered | `USequenceTests`, `USequenceBuildOrderTests`, `USequenceTraversalTests`, `USequenceLifecycleTests` | Constructor is exercised directly by multiple integration-style tests. |
| `void RestoreDynamic()` | Covered | `USequenceTests.RestoreDynamic_Indexes_Records_Appended_After_Last_Saved_State` | Dedicated dynamic restore contract test exists. |
| `void Clear()` | Covered | `USequenceLifecycleTests.Clear_Resets_Visible_Sequence_State_And_Keeps_Object_Reusable` | Direct lifecycle test now asserts reset + reusability. |
| `void Flush()` | Covered | `USequenceLifecycleTests.Flush_Persists_Current_State_File_Without_Build` | Direct lifecycle test now asserts persistence without `Build()`. |
| `void Close()` | Covered | `USequenceLifecycleTests.Close_Flushes_State_And_Reopen_Remains_Consistent` | Direct lifecycle test now asserts close + reopen consistency. |
| `void Refresh()` | Covered | `USequenceBuildOrderTests`, reopen/traversal tests | Reopen/refresh consistency is directly asserted. |
| `void Load(IEnumerable<object> flow)` | Covered | `USequenceTests.Load_Skips_Empty_Records_And_Writes_State_File` | Direct loading behavior and empty-record filtering are asserted. |
| `IEnumerable<object> ElementValues()` | Covered | `USequenceTests`, `USequenceTraversalTests`, `USequenceLifecycleTests` | Directly asserts filtering of empty/superseded records and lifecycle visibility. |
| `void Scan(Func<long, object, bool> handler)` | Covered | `USequenceTests.ElementValues_And_Scan_Use_Only_Latest_Duplicate_Key`, `USequenceTraversalTests` | Direct filtered traversal behavior is asserted. |
| `long AppendElement(object element)` | Covered | `USequenceTests`, `USequenceBuildOrderTests`, `USequenceLifecycleTests` | Append is directly exercised and asserted across integration scenarios. |
| `void CorrectOnAppendElement(long off)` | Covered | `USequenceTests.CorrectOnAppendElement_Indexes_Record_Added_Directly_To_Base_Sequence` | Direct helper/index-correction contract test exists. |
| `object GetByKey(IComparable keysample)` | Covered | `USequenceTests`, `USequenceBuildOrderTests` | Primary-key lookup is directly tested. |
| `IEnumerable<object> GetAllByValue(int nom, IComparable value, Func<object, IEnumerable<IComparable>> keysFunc, bool ignorecase = false)` | Covered | `USequenceBuildOrderTests.Build_FlushesSequence_BuildsAndPersistsIndexes_SavesState_And_ReopenRemainsConsistent` | Real secondary index lookup behavior is asserted. |
| `IEnumerable<object> GetAllBySample(int nom, object osample)` | Covered | `USequenceBuildOrderTests` | Direct sample-based lookup test exists. |
| `IEnumerable<object> GetAllByLike(int nom, object sample)` | Covered | `USequenceLikeTests.GetAllByLike_Returns_Expected_Matches_Excludes_Superseded_And_Empty_Records` | Dedicated integration test now exists. |
| `void Build()` | Covered | `USequenceTests.Build_Writes_State_And_GetByKey_Returns_Value`, `USequenceBuildOrderTests` | Build order, state save, and reopen/index consistency are asserted. |

#### RecordAccessor
| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `RecordAccessor(PTypeRecord recordType)` | Covered | `RecordAccessorTests` | Constructor is exercised throughout the test suite. |
| `PTypeRecord RecordType` | Covered indirectly | fixture usage | The property is used conceptually, but a direct property-level assertion is still desirable if needed. |
| `int FieldCount` | Covered indirectly | `RecordAccessorTests.CreateRecord_Creates_Array_With_Expected_Field_Count` | The property is implied through create/shape assertions, but not strongly isolated as a property contract. |
| `IEnumerable<string> FieldNames` | Covered | `RecordAccessorTests.FieldNames_Preserve_Schema_Order` | Direct order-preservation test exists. |
| `bool HasField(string fieldName)` | Covered | `RecordAccessorTests.HasField_Returns_True_For_Existing_Field_And_False_For_Missing_Field`, `HasField_Throws_ArgumentNullException_For_Null` | Direct positive/negative/null coverage exists. |
| `int GetIndex(string fieldName)` | Covered | `RecordAccessorTests.GetIndex_Returns_Stable_Field_Position`, `GetIndex_Throws_ArgumentNullException_For_Null` | Direct lookup and null-argument coverage exists. |
| `PType GetFieldType(string fieldName)` | Covered | `RecordAccessorTests.GetFieldType_Returns_Declared_Field_Type` | Direct declared-type lookup test exists. |
| `object[] CreateRecord()` | Covered indirectly | empty/minimal record tests | Parameterless create is exercised through empty-record cases, but not as a standalone focused contract. |
| `object[] CreateRecord(params object[] values)` | Covered | multiple `RecordAccessorTests` | Correct creation, null, and wrong-count cases are asserted. |
| `void ValidateShape(object record)` | Covered | `RecordAccessorTests.ValidateShape_Throws_On_Invalid_Field_Count`, `ValidateShape_Throws_On_NonObjectArray` | Direct shape-validation negative coverage exists. |
| `object Get(object record, string fieldName)` | Covered | `RecordAccessorTests.Get_And_Set_By_Field_Name_Work` | Direct generic field get is asserted. |
| `T Get<T>(object record, string fieldName)` | Covered | multiple `RecordAccessorTests` | Typed getter is directly exercised. |
| `void Set(object record, string fieldName, object value)` | Covered | multiple `RecordAccessorTests` | Direct setter coverage exists. |
| `bool TryGet(object record, string fieldName, out object value)` | Covered | `RecordAccessorTests.TryGet_Returns_False_For_Missing_Field` | Direct non-throwing missing-field behavior is asserted. |
| `bool TryGet<T>(object record, string fieldName, out T value)` | Covered | `RecordAccessorTests.TryGet_Typed_Returns_True_For_Correct_Type`, `TryGet_Typed_Returns_False_For_Wrong_Type` | Direct typed success/mismatch behavior is asserted. |

#### ByteFlow
| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `static void Serialize(BinaryWriter bw, object v, PType tp)` | Covered | `ByteFlowTests` | Primitive branches (`boolean`, `byte`, `character`, `integer`, `longinteger`, `none`, `sstring` null), record, sequence, union, nested cases, and some negative cases are asserted. |
| `static object Deserialize(BinaryReader br, PType tp)` | Covered | `ByteFlowTests` | Primitive branches, record, sequence, union, nested cases, and negative sequence-length behavior are asserted. |

#### TextFlow
| Method | Status | Evidence | Notes |
|--------|--------|----------|-------|
| `static void Serialize(TextWriter tw, object v, PType tp)` | Covered | `TextFlowTests`, `TextFlowPrimitiveTests` | Record/union plus primitive textual branches are directly asserted. |
| `static void SerializeFormatted(TextWriter tw, object v, PType tp, int level)` | Covered | `TextFlowTests.SerializeFormatted_ForNestedRecord_AddsLineBreaks` | Nested formatting contract is directly asserted. |
| `static void SerializeFlowToSequense(TextWriter tw, IEnumerable<object> flow, PType tp)` | Covered | `TextFlowTests.SerializeFlowToSequense_And_DeserializeSequenseToFlow_RoundTrip` | Direct sequence serialization round-trip exists. |
| `static void SerializeFlowToSequenseFormatted(TextWriter tw, IEnumerable<object> flow, PType tp, int level)` | Covered | `TextFlowPrimitiveTests.SerializeFlowToSequenseFormatted_Produces_Readable_Multiline_Output` | Direct formatted sequence-output test exists. |
| `static object Deserialize(TextReader tr, PType tp)` | Covered | `TextFlowTests`, `TextFlowPrimitiveTests` | Positive parsing of record, string, union, and primitive branches is directly asserted. |
| `static IEnumerable<object> DeserializeSequenseToFlow(TextReader tr, PType tp)` | Covered | `TextFlowTests.SerializeFlowToSequense_And_DeserializeSequenseToFlow_RoundTrip` | Positive textual sequence parsing is directly asserted. |
| parser robustness for malformed textual input | Partially covered | existing positive/escape tests only | Dedicated negative tests for malformed input are still missing and should target the public entry points above. |

### Strongly covered areas
- `UniversalSequenceBase` core behavior: append, overwrite, refresh/recovery, traversal helpers, sorting.
- `USequence` build/traversal/index usage, dynamic restore, and now direct lifecycle coverage (`Clear`, `Flush`, `Close`).
- `RecordAccessor` main ergonomic API plus helper/tolerant methods.
- `ByteFlow` primitive and composite binary serialization round-trips.
- `TextFlow` positive serialization/deserialization flows and formatted output.

### Partially covered or missing areas
- Dedicated direct property tests for `RecordAccessor.RecordType` and `RecordAccessor.FieldCount` are still optional but not critical.
- `UniversalSequenceBase.Close()` is still better described as indirectly covered unless a direct focused contract test is added.
- `TextFlow` malformed-input / negative parser coverage is still the clearest remaining testing gap.

### Honest repository-level summary
The repository now has strong regression coverage for the repaired behaviors that matter most: storage recovery/refresh, append-offset discipline, overwrite boundaries, key-index boundary behavior, record ergonomics, build order, lifecycle stability, and core serialization paths.

What is **not** yet proven is full parser-proof robustness for malformed textual input in `TextFlow`, and a few smaller direct-public-contract/property tests remain optional polish rather than core correctness gaps.
