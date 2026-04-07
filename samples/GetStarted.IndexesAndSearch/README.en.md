# GetStarted.IndexesAndSearch

Step 3 of the public PolarDB tutorial path.

This project demonstrates the compact public indexing/search surface.

The index implementations in this step (`UIndex`, `SVectorIndex`, `UVectorIndex`, `UVecIndex`) all follow the shared `IUIndex` contract, but tutorials focus on behavior of concrete indexes rather than interface internals.
`ObjOff` is a low-level result transport primitive used inside index pipelines; it is covered by tests and intentionally not taught as a standalone scenario.

## Scenario IDs

- `primary-key` - primary-key lookup and dynamic append in `USequence`.
- `age-index` - exact secondary lookup via `UIndex`.
- `text-search` - token and prefix search with `SVectorIndex`.
- `tag-vector` - exact multi-value lookup with `UVectorIndex`.
- `skill-hash` - hash-based multi-value lookup with `UVecIndex`.
- `scale` - approximate search windows via `Scale` and `Diapason`.
- `hash-functions` - deterministic helper hashes for index scenarios.

## Run

```bash
dotnet run --project samples/GetStarted.IndexesAndSearch -- list
dotnet run --project samples/GetStarted.IndexesAndSearch -- all
dotnet run --project samples/GetStarted.IndexesAndSearch -- primary-key
```
