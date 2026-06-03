# Samples

Public tutorial projects for Polar.DB are organized as a three-step learning path:

1. `GetStarted.StructuresAndSerialization`
2. `GetStarted.SequencesAndStorage`
3. `GetStarted.IndexesAndSearch`

Start from project 1 and move forward in order.

## Run commands

```bash
dotnet run --project samples/GetStarted.StructuresAndSerialization -- list
dotnet run --project samples/GetStarted.StructuresAndSerialization -- all
dotnet run --project samples/GetStarted.StructuresAndSerialization -- fstring
```

```bash
dotnet run --project samples/GetStarted.SequencesAndStorage -- list
dotnet run --project samples/GetStarted.SequencesAndStorage -- all
dotnet run --project samples/GetStarted.SequencesAndStorage -- recovery-refresh
```

```bash
dotnet run --project samples/GetStarted.IndexesAndSearch -- list
dotnet run --project samples/GetStarted.IndexesAndSearch -- all
dotnet run --project samples/GetStarted.IndexesAndSearch -- primary-key
```

## Notes

- Samples focus on current public APIs under `Polar.DB`.
- Projects are intentionally compact and educational.
- Runtime files are created under each sample output folder.

