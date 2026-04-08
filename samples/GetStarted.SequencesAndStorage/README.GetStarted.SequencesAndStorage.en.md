# GetStarted.SequencesAndStorage

This is the second tutorial project in the Polar.DB samples.

It builds on the basics from `GetStarted.StructuresAndSerialization` and moves from value shape to storage behavior.

## What this project teaches

- how records are stored in sequences;
- how append-oriented workflows look in practice;
- how `Flush`, `Scan`, and sequential traversal fit together;
- how offsets can be used for direct access or helper lookup structures;
- how sequence-oriented code differs from purely in-memory object examples.

## Why this project matters

Polar.DB is not only about schemas. It is also about reliable structured storage.

This project is where the reader should start thinking about:
- logical data boundaries;
- append flow versus read flow;
- persistent layout rather than only temporary objects in memory.

## Suggested progression

1. start with a simple universal sequence example;
2. continue with scanning and append scenarios;
3. then study offset-oriented scenarios;
4. only after that move to the dedicated index/search tutorial.

## Typical commands

```bash
dotnet run --project samples/GetStarted.SequencesAndStorage -- list
dotnet run --project samples/GetStarted.SequencesAndStorage -- all
dotnet run --project samples/GetStarted.SequencesAndStorage -- <scenario-id>
```

## Notes

This project should stay focused on storage and sequence mechanics.

Full index tutorials, token search, and multi-value lookup belong to `GetStarted.IndexesAndSearch`.

