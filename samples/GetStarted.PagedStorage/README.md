# GetStarted.PagedStorage

Этот проект собирает в одном месте сценарии из старых файлов `samples/GetStarted1/Program4.cs` … `Program9.cs`, которые относятся к страничному хранению, `PagedStream`, `StreamStorage`, `TableView`, ручным key/offset индексам и бенчмаркам записи.

Что сохранено:
- исходные комментарии рядом с кодом;
- исходные статические helper-методы и вложенные классы;
- отдельные сценарии в отдельных `.cs` файлах.

Что изменено:
- общий запуск теперь идёт из одного `Program.cs`;
- абсолютный `dbpath` заменён на локальную папку `data/PagedStorage/` рядом с выходом программы;
- каждый старый файл оформлен как отдельный `ISampleScenario`.

Команды:
- `dotnet run -- list`
- `dotnet run -- gs1-p4`
- `dotnet run -- all`

Сценарии:
- `gs1-p4` — таблица и универсальный индекс поверх `PagedStorage`
- `gs1-p5` — три последовательности и ручной key/offset индекс
- `gs1-p6` — строковый идентификатор и half-key индекс
- `gs1-p7` — key-value storage поверх paged streams
- `gs1-p8` — продвинутый paged key-value сценарий с portions
- `gs1-p9` — бенчмарки записи для stream / `PaCell` / `StreamStorage`

Замечание: этот zip рассчитан на размещение проекта рядом с остальными sample-проектами репозитория, поэтому `ProjectReference` настроены в стиле старых `GetStarted`-проектов.
