# GetStarted.IndexesAndSearch

Шаг 3 публичного учебного пути PolarDB.

Проект показывает компактную публичную поверхность индексов и поиска.

Реализации индексов в этом шаге (`UIndex`, `SVectorIndex`, `UVectorIndex`, `UVecIndex`) используют общий контракт `IUIndex`, но в обучении акцент сделан на поведение конкретных индексов, а не на внутренности интерфейса.
`ObjOff` — низкоуровневый транспортный примитив результатов в индексном конвейере; он покрыт тестами и намеренно не вынесен в отдельный учебный сценарий.

## Идентификаторы сценариев

- `primary-key` - поиск по первичному ключу и динамический append в `USequence`.
- `age-index` - точный secondary lookup через `UIndex`.
- `text-search` - поиск по токену и префиксу через `SVectorIndex`.
- `tag-vector` - точный поиск по множественным значениям через `UVectorIndex`.
- `skill-hash` - hash-based поиск по множественным значениям через `UVecIndex`.
- `scale` - приблизительные окна поиска через `Scale` и `Diapason`.
- `hash-functions` - детерминированные вспомогательные hash-функции.

## Запуск

```bash
dotnet run --project samples/GetStarted.IndexesAndSearch -- list
dotnet run --project samples/GetStarted.IndexesAndSearch -- all
dotnet run --project samples/GetStarted.IndexesAndSearch -- primary-key
```
