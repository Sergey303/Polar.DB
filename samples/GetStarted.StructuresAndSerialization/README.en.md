# GetStarted.StructuresAndSerialization

Step 1 of the public PolarDB tutorial path.

This project explains schema types, object-shaped values, and serialization basics.

## Scenario IDs

- `gs-legacy` - baseline extracted intro from the legacy sample.
- `gs5-intro` - baseline intro to record/sequence shape and text serialization.
- `gs1-demo101` - extracted historical intro fragment with `PType` and `TextFlow`.
- `gs2-201` - types and serialization walkthrough.
- `gs3-301` - object representation and text round-trip.
- `gs4-401` - alternate serialization walkthrough.
- `fstring` - `PTypeFString` schema round-trip (`PType -> object -> PType`).
- `union-byteflow` - `PTypeUnion` with `ByteFlow.Serialize`/`Deserialize`.
- `record-accessor` - named get/set mutation using `RecordAccessor`.

## Run

```bash
dotnet run --project samples/GetStarted.StructuresAndSerialization -- list
dotnet run --project samples/GetStarted.StructuresAndSerialization -- all
dotnet run --project samples/GetStarted.StructuresAndSerialization -- record-accessor
```
