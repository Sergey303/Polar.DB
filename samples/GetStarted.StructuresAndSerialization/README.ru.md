# GetStarted.StructuresAndSerialization

Шаг 1 публичного учебного пути Polar.DB.

Проект показывает схемы типов, объектное представление значений и основы сериализации.

`NamedType` во всех сценариях используется как механизм именования частей схемы (например, полей `PTypeRecord` и вариантов `PTypeUnion`).

## Идентификаторы сценариев

- `gs-legacy` - базовый извлеченный вводный сценарий из старого sample.
- `gs1-demo101` - вводный фрагмент с `PType` и `TextFlow`.
- `gs2-201` - обзор типов и сериализации.
- `gs3-301` - объектное представление и текстовый round-trip.
- `gs4-401` - альтернативный вариант walkthrough по сериализации.
- `gs5-intro` - вводный сценарий в стиле package-based sample.
- `fstring` - round-trip схемы `PTypeFString` (`PType -> object -> PType`).
- `union-byteflow` - `PTypeUnion` + `ByteFlow.Serialize`/`Deserialize`.
- `record-accessor` - именованный get/set через `RecordAccessor`.

## Запуск

```bash
dotnet run --project samples/GetStarted.StructuresAndSerialization -- list
dotnet run --project samples/GetStarted.StructuresAndSerialization -- all
dotnet run --project samples/GetStarted.StructuresAndSerialization -- record-accessor
```

