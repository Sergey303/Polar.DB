# GetStarted.IndexesAndSearch

A compact tutorial project for the current Polar.DB index/search surface.

## What this sample covers

1. Primary-key lookup through `USequence`.
2. Secondary exact lookup through `UIndex`.
3. Text token and prefix search through `SVectorIndex`.
4. Exact multi-value lookup through `UVectorIndex`.
5. Hash-based multi-value lookup through `UVecIndex`.
6. Approximate search windows through `Scale` and `Diapason`.
7. Utility hash functions used in indexing examples.

## Run

```bash
dotnet run --project samples/GetStarted.IndexesAndSearch/GetStarted.IndexesAndSearch -- list
dotnet run --project samples/GetStarted.IndexesAndSearch/GetStarted.IndexesAndSearch -- all
dotnet run --project samples/GetStarted.IndexesAndSearch/GetStarted.IndexesAndSearch -- primary-key
```

## Notes

- The project writes scenario data under `bin/.../data/IndexesAndSearch/<scenario-id>/`.
- Every scenario recreates its own folder, so reruns start from a clean state.
- All records are stored as Polar records (`object[]`) and accessed through `RecordAccessor`.

## Russian note / Русское пояснение

Это не legacy-архив и не миграционный остаток. Это учебный проект по актуальной индексной поверхности Polar.DB:

- первичный поиск по ключу;
- вторичный индекс по значению;
- текстовый поиск по токенам и префиксу;
- поиск по множественным значениям;
- hash-based поиск по множественным значениям;
- `Scale` / `Diapason` как вспомогательный механизм для приблизительного диапазона.
