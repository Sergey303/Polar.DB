# Samples and Test Coverage Audit

This audit maps the maintained `Polar.DB` surface to:
- direct/indirect tests in `tests/Polar.DB.Tests`
- public examples in the 3 tutorial projects under `samples`

Scope used for this audit:
- `src/Polar.DB`
- `tests/Polar.DB.Tests`
- `samples/GetStarted.StructuresAndSerialization`
- `samples/GetStarted.SequencesAndStorage`
- `samples/GetStarted.IndexesAndSearch`

## Coverage Matrix

| Name | Public/Internal | Tests | Sample | Action required | Notes |
|---|---|---|---|---|---|
| `PType` | public | direct | yes | none | Covered in `TestPType`, `TypesRoundTripTests`; demonstrated in `gs-*` scenarios. |
| `PTypeRecord` | public | direct | yes | none | Direct in `TestPType`, `TypesRoundTripTests`, `RecordAccessorTests`. |
| `PTypeSequence` | public | direct | yes | none | Direct in `ByteFlowTests`, `TypesRoundTripTests`; shown in structures and sequences samples. |
| `PTypeUnion` | public | direct | yes | none | Direct in `ByteFlowTests`, `TextFlowTests`, `TypesRoundTripTests`; shown in `union-byteflow`. |
| `PTypeFString` | public | direct | yes | none | Direct schema round-trip tests plus `fstring` sample. |
| `NamedType` | public | indirect | yes | justify omission | Meaningfully exercised as schema building block through all `PType*` tests/samples. |
| `PTypeEnumeration` | public | indirect | yes | justify omission | Covered via `PType`/serializer behavior; no standalone tests needed. |
| `TextFlow` | public | direct | yes | none | Direct tests in `TextFlowTests`; shown in structures scenarios. |
| `ByteFlow` | public | direct | yes | none | Direct tests in `ByteFlowTests`; shown in `union-byteflow`. |
| `RecordAccessor` | public | direct | yes | none | `RecordAccessorTests` + `record-accessor` sample. |
| `UniversalSequenceBase` | public | direct | yes | none | Extensive direct tests + multiple step-2 scenarios. |
| `USequence` | public | direct | yes | none | Direct tests in `USequenceTests`/`UKeyIndexTests`; shown in step-2 and `primary-key`. |
| `IUIndex` | public | indirect | yes | justify omission | Interface is exercised through all implementations (`SVectorIndex`, `UIndex`, `UVectorIndex`, `UVecIndex`) in tests/samples. |
| `UIndex` | public | direct | yes | none | Direct behavioral tests (exact + repeated-key retrieval) in `SecondaryIndexesTests`; shown in `age-index`. |
| `SVectorIndex` | public | direct | yes | none | Direct in `SecondaryIndexesTests`; shown in `text-search`. |
| `UVectorIndex` | public | direct | yes | none | Direct in `SecondaryIndexesTests`; shown in `tag-vector`. |
| `UVecIndex` | public | direct | yes | none | Direct in `SecondaryIndexesTests`; shown in `skill-hash`. |
| `ObjOff` | public | direct | no | justify omission | Low-level index result carrier; tested in `ScaleAndUtilitiesTests`, not useful as standalone tutorial concept. |
| `Scale` | public | direct | yes | none | Covered by `ScaleAndUtilitiesTests` (32/64 and persisted scale); shown in `scale`. |
| `Diapason` | public | direct | yes | none | Covered in `ScaleAndUtilitiesTests`; shown in `scale`. |
| `Hashfunctions` | public | direct | yes | none | Deterministic tests + dedicated `hash-functions` sample. |
| `UKeyIndex` | internal | indirect | no | none | Intentionally internal; exercised through `USequence` behavior (`UKeyIndexTests`, `USequenceTests`). |
| `HKeyObjOff` | internal | indirect | no | none | Internal helper in `UIndex`; covered through `UIndex` behavior tests. |
| `PTypeEnumeration.objPair` branch | obsolete/unimplemented | none | no | justify omission | Explicitly unimplemented in `PType.FromPObject`; not part of maintained public behavior. |

## Indirect-Only Justifications

- `NamedType`, `PTypeEnumeration`, and `IUIndex` are structural abstractions. Their behavior is validated through higher-level, externally observable contracts.
- `UKeyIndex` and `HKeyObjOff` are internal implementation details intentionally accessed only via public `USequence`/index APIs.
- `ObjOff` is public for transport between internal index layers and query pipelines; it is tested but intentionally not taught as a user-facing API.

## Topic-to-Test-and-Sample Correspondence

| Topic | Tests | Sample scenarios | Comments |
|---|---|---|---|
| Type schema and object representation (`PType*`, `ToPObject`, `FromPObject`, `Interpret`) | `TestPType`, `TypesRoundTripTests` | `gs-legacy`, `gs1-demo101`, `gs2-201`, `gs3-301`, `gs4-401`, `gs5-intro`, `fstring`, `union-byteflow` | Strong direct pairing. |
| Text serialization (`TextFlow`) | `TextFlowTests`, `TestPType` | `gs-*` structures scenarios | Explicit text round-trip coverage. |
| Binary serialization (`ByteFlow`) | `ByteFlowTests`, `TestPType` | `union-byteflow` | Explicit binary round-trip plus nested shapes. |
| Named record operations (`RecordAccessor`) | `RecordAccessorTests` | `record-accessor` | 1:1 correspondence. |
| Base storage/recovery (`UniversalSequenceBase`) | `UniversalSequenceBaseTests` | `gs-legacy-seq`, `gs1-demo101-seq`, `gs3-303`, `gs3-305`, `gs3-306`, `recovery-refresh` | Includes reopen/refresh/tail normalization. |
| Primary key and dynamic state (`USequence` + internal `UKeyIndex`) | `USequenceTests`, `UKeyIndexTests` | `gs-legacy-seq`, `primary-key`, `recovery-refresh` | Covers append visibility, state restore, refresh continuity. |
| Secondary exact and vector indexes (`UIndex`, `SVectorIndex`, `UVectorIndex`, `UVecIndex`) | `SecondaryIndexesTests` | `age-index`, `text-search`, `tag-vector`, `skill-hash` | Includes exact lookup and repeated keys; dynamic append visibility is covered for vector index implementations. |
| Search helpers (`Scale`, `Diapason`, `Hashfunctions`) | `ScaleAndUtilitiesTests`, `TestPType` | `scale`, `hash-functions` | Deterministic utility-level verification plus examples. |
| Interface/transport internals (`IUIndex`, `ObjOff`) | `SecondaryIndexesTests`, `ScaleAndUtilitiesTests` | indirect via index scenarios | Kept intentionally non-tutorial as standalone concepts. |

## Remaining Justified Omissions

- No standalone public tutorial scenario for `ObjOff`, `IUIndex`, `NamedType`, or internal index internals (`UKeyIndex`, `HKeyObjOff`): these are either abstractions/internals or low-level transport primitives better demonstrated through complete workflows.
- No coverage target for `PTypeEnumeration.objPair`: currently unimplemented and treated as obsolete/non-maintained surface.
