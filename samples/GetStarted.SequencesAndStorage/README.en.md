# GetStarted.SequencesAndStorage

Step 2 of the public Polar.DB tutorial path.

This project focuses on sequence storage, offsets, and recovery-oriented behavior.

## Scenario IDs

- `gs-legacy-seq` - extracted walkthrough of `UniversalSequenceBase` and `USequence`.
- `gs1-demo101-seq` - append + scan on `UniversalSequenceBase`.
- `gs3-303` - keys/offset arrays with `Array.BinarySearch`.
- `gs3-305` - custom first-match binary search over keys/offsets.
- `gs3-306` - persisted keys/offset sequences for lookup.
- `recovery-refresh` - reopen, `Refresh()`, and continued append from logical tail.

## Run

```bash
dotnet run --project samples/GetStarted.SequencesAndStorage -- list
dotnet run --project samples/GetStarted.SequencesAndStorage -- all
dotnet run --project samples/GetStarted.SequencesAndStorage -- recovery-refresh
```

