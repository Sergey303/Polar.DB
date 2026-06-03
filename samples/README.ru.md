# Samples

Публичные учебные проекты Polar.DB организованы как путь из трех шагов:

1. `GetStarted.StructuresAndSerialization`
2. `GetStarted.SequencesAndStorage`
3. `GetStarted.IndexesAndSearch`

Рекомендуется проходить их именно в этом порядке.

## Команды запуска

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

## Заметки

- Примеры сфокусированы на актуальных публичных API из `Polar.DB`.
- Проекты намеренно компактные и учебные.
- Runtime-файлы создаются в выходной директории соответствующего sample-проекта.

