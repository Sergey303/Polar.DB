# GetStarted.IndexesAndSearch

This is the third tutorial project in the Polar.DB samples.

It focuses on the current lookup and search surface after the reader already understands types, serialization, sequences, and storage basics.

## What this project teaches

- primary-key lookup through `USequence`;
- secondary exact lookup through `UIndex`;
- token and prefix search through `SVectorIndex`;
- exact multi-value lookup through `UVectorIndex`;
- hash-based multi-value lookup through `UVecIndex`;
- helper utilities such as `Scale`, `Diapason`, and example hash functions.

## Why this project exists

Indexes are easier to understand after storage basics are clear.

This project is not about abstract theory alone. It is about seeing how real lookup scenarios are expressed in Polar.DB code.

## Recommended progression inside this project

1. primary key scenario;
2. secondary exact lookup;
3. text token and prefix search;
4. multi-value lookup;
5. helper utilities and supporting primitives.

## Typical commands

```bash
dotnet run --project samples/GetStarted.IndexesAndSearch -- list
dotnet run --project samples/GetStarted.IndexesAndSearch -- all
dotnet run --project samples/GetStarted.IndexesAndSearch -- <scenario-id>
```

## Notes

Keep this project practical.

Examples should show how to use the current API clearly and with small datasets that are easy to inspect from the console output.

