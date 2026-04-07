# GetStarted.StructuresAndSerialization

Этот проект — первый тематический срез для реорганизации `samples/GetStarted*`.

Что внутри:
- новый `Program.cs`, который показывает список сценариев и умеет запускать один или все;
- старые примеры, переписанные под формат сценариев;
- для смешанных файлов перенесена только та часть, которая относится к структурам и сериализации;
- комментарии вокруг перенесённых фрагментов сохранены максимально близко к исходному коду.

Что сознательно не вошло:
- `UniversalSequenceBase`;
- индексы, потоки, triple store и другие отдельные темы.

Они должны переехать в другие тематические проекты.

Запуск:
- `dotnet run --project samples/GetStarted.StructuresAndSerialization -- list`
- `dotnet run --project samples/GetStarted.StructuresAndSerialization -- all`
- `dotnet run --project samples/GetStarted.StructuresAndSerialization -- gs2-201`
