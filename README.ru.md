# Polar.DB

Polar.DB — это библиотека .NET для работы со структурированными данными с явно заданной схемой, бинарной сериализацией, append-ориентированными постоянными последовательностями и индексным поиском по типизированным записям.

Проект вырос из работ, начатых в 2013 году вокруг типизированного хранения данных и экспериментов с RDF-хранилищами большого объёма. Сейчас репозиторий сфокусирован на переиспользуемой библиотеке .NET с явными схемами (`PType*`), последовательностями, именованным доступом к полям через `RecordAccessor` и сценариями индексации и поиска.

## Статус проекта

**Experimental / pre-production**

Polar.DB уже подходит для исследований, прототипов, внутренних инструментов и контролируемых embedded-сценариев хранения. При этом проект **ещё не позиционируется как полностью production-hardened универсальная СУБД**.

## Для чего Polar.DB подходит

- типизированные структурные значения и записи;
- локальное append-ориентированное хранение;
- бинарная сериализация данных по схеме;
- последовательности и сценарии работы поверх них;
- индексный доступ к сохранённым данным;
- эксперименты, исследования и специализированные встроенные хранилища;
- сценарии, где допустимы `object[]`-записи или более безопасный именованный доступ через `RecordAccessor`.

## Для чего Polar.DB пока не подходит

- распределённая СУБД;
- замена PostgreSQL / SQL Server / SQLite;
- транзакционный ACID-движок со зрелыми гарантиями конкурентного доступа;
- критичные rewrite-heavy нагрузки без валидации конкретного пути хранения;
- случаи, где уже сегодня нужен полностью замороженный и долгосрочно стабильный публичный API.

## Поддерживаемые target frameworks

Текущая библиотека таргетит:

- `netstandard2.0`
- `netstandard2.1`
- `netcoreapp3.1`
- `net5.0`
- `net6.0`
- `net7.0`
- `net8.0`
- `net9.0`
- `net10.0`

## Быстрый старт

Установка пакета:

```bash
dotnet add package Polar.DB
```

Минимальный пример со схемой и append-ориентированной последовательностью:

```csharp
using Polar.DB;

var personType = new PTypeRecord(
    new NamedType("id", new PType(PTypeEnumeration.integer)),
    new NamedType("name", new PType(PTypeEnumeration.sstring)),
    new NamedType("age", new PType(PTypeEnumeration.integer)));

var accessor = new RecordAccessor(personType);

using var stream = new MemoryStream();
var sequence = new UniversalSequenceBase(personType, stream);

sequence.Clear();
sequence.AppendElement(accessor.CreateRecord(1, "Alice", 30));
sequence.Flush();

var person = (object[])sequence.GetElement(8L);

Console.WriteLine(
    $"{accessor.Get<int>(person, "id")} " +
    $"{accessor.Get<string>(person, "name")} " +
    $"{accessor.Get<int>(person, "age")}");
```

## Ключевые идеи

- `PType`, `PTypeRecord`, `PTypeSequence` и `PTypeUnion` задают схему данных.
- Значения Polar в рантайме часто представлены как `object` и `object[]`, согласованные со схемой.
- `RecordAccessor` даёт доступ к полям записи по имени и помогает уйти от магических индексов.
- Хранение последовательностей ориентировано на append и опирается на **логическую границу валидных данных**, а не на случайное положение курсора.
- Сценарии индексации и поиска поддерживаются текущими API и sample-проектами.

## Сборка и тесты

Репозиторий фиксирует SDK через `global.json`.

```bash
dotnet restore
dotnet build Polar.DB.sln
dotnet test tests/Polar.DB.Tests/Polar.DB.Tests.csproj
```

## Примеры

Готовые примеры лежат в `samples/`:

- `GetStarted.StructuresAndSerialization`
- `GetStarted.SequencesAndStorage`
- `GetStarted.IndexesAndSearch`

## Документация

Начинать удобно отсюда:

- `docs/Starting.md` — практический вход
- `docs/About.md` — история проекта, публикации, участники
- `docs/Description.md` — более общее описание системы
- `docs/Specifications.md` — технические заметки и спецификации
- `docs/REPOSITORY_STATE.md` — текущее состояние репозитория и инварианты

## Известные ограничения и важные оговорки

- Модель хранения опирается на **логический конец валидных данных**, а не просто на `Stream.Position`.
- `AppendOffset` нужно трактовать как нормализованную логическую позицию для append.
- Физическая длина файла не всегда совпадает с логическим концом данных в recovery-сценариях.
- Логика recovery и refresh стала строже, но это не делает автоматически безопасными любые rewrite-сценарии.
- В частности, **к variable-size in-place overwrite нужно относиться осторожно**.
- Внешний lifecycle sidecar-файла состояния (`stateFileName`) ещё требует архитектурной доработки.
- Публичный API и документация улучшаются, но в репозитории пока сосуществуют исторические и современные слои.

## Совместимость и миграция

- В старых материалах может встречаться legacy-имя `PolarDB` без точки.
- В старых документах и metadata могут оставаться исторические ссылки на внешний репозиторий.
- Во всех новых публичных материалах желательно использовать каноническое имя **Polar.DB**.

## Вклад в проект

Перед pull request прочитайте [CONTRIBUTING.md](CONTRIBUTING.md).

## Безопасность

Перед сообщением об уязвимости прочитайте [SECURITY.md](SECURITY.md).

## Поддержка

Для вопросов по использованию, багов и feature request см. [SUPPORT.md](SUPPORT.md).

## Лицензия

Проект распространяется по лицензии MIT. См. [LICENSE](LICENSE).
