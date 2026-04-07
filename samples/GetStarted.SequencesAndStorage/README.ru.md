# GetStarted.SequencesAndStorage

Шаг 2 публичного учебного пути PolarDB.

Проект посвящен хранению в последовательностях, работе со смещениями и сценариям восстановления.

## Идентификаторы сценариев

- `gs-legacy-seq` - извлеченный walkthrough по `UniversalSequenceBase` и `USequence`.
- `gs1-demo101-seq` - append + scan на `UniversalSequenceBase`.
- `gs3-303` - массивы ключей/смещений и `Array.BinarySearch`.
- `gs3-305` - собственный first-match binary search по ключам/смещениям.
- `gs3-306` - хранение keys/offsets в отдельных последовательностях.
- `recovery-refresh` - reopen, `Refresh()` и продолжение append от логического хвоста.

## Запуск

```bash
dotnet run --project samples/GetStarted.SequencesAndStorage -- list
dotnet run --project samples/GetStarted.SequencesAndStorage -- all
dotnet run --project samples/GetStarted.SequencesAndStorage -- recovery-refresh
```
