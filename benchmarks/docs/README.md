# Benchmarks

Primary index build now uses bulk writes for the two fixed-size index arrays:

- `hkeys` uses `UniversalSequenceBase.ReplaceWithFixedInt32Array`;
- `offsets` uses `UniversalSequenceBase.ReplaceWithFixedInt64Array`.

This avoids millions of `UniversalSequenceBase.AppendElement` calls and bypasses
generic `ByteFlow.Serialize(object, PType)` for primary index build output.

Run the diagnostic experiment:

```powershell
dotnet run -c Release --project .\benchmarks\src\BuildPrimaryIntOnly\BuildPrimaryIntOnly.csproj
```

The `Polar.DB primary index build internals` table should show much smaller
`Write hash keys` and `Write offsets` values.
