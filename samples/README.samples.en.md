# Samples

This folder contains the public tutorial samples for Polar.DB.

The goal of these projects is not to preserve every historical experiment. Their goal is to provide a clear learning path through the current public surface of the repository.

## Learning order

1. **GetStarted.StructuresAndSerialization**
   Start here if you are new to Polar.DB. This project introduces Polar types, object-shaped values, interpretation, and text/binary serialization.

2. **GetStarted.SequencesAndStorage**
   Continue here after the first project. This project shows how records are stored in sequences, how append/flush/scan work, and how offset-based access fits into the storage model.

3. **GetStarted.IndexesAndSearch**
   Finish here. This project demonstrates the current indexing and lookup surface: primary-key lookup, secondary indexes, text search, vector-style multi-value lookup, and helper utilities such as `Scale` and `Diapason`.

## Design principles of these samples

- Each project is thematic.
- Scenarios are small enough to read in one sitting.
- Runtime data is created locally under the sample output folders.
- The samples are meant to explain the library, not to hide it behind a framework.

## What is intentionally not here

This folder is the public tutorial surface. It is not the right place for legacy wrappers, migration leftovers, or large one-off experiments.

If some old code is still useful for archaeology or comparison, it should live outside the main tutorial path.

## Typical commands

```bash
dotnet run --project samples/GetStarted.StructuresAndSerialization -- list
dotnet run --project samples/GetStarted.SequencesAndStorage -- list
dotnet run --project samples/GetStarted.IndexesAndSearch -- list
```

Most sample projects follow the same command style:

```bash
dotnet run -- list
dotnet run -- all
dotnet run -- <scenario-id>
```

## Notes for contributors

Keep the public samples simple, current, and thematic.

When adding a new scenario:
- prefer one clear idea per scenario;
- keep comments useful for a first-time reader;
- avoid duplicating another scenario with only cosmetic differences;
- prefer current APIs and current namespaces.

