# GetStarted.TripleStore

Этот проект собирает вместе старые triple/triple-store сценарии из `samples/GetStarted1`.

Что внутри:
- новый `Program.cs`, который умеет показывать список сценариев и запускать их по id;
- старые сценарные файлы, перенесённые почти без изменений;
- сохранены исходные комментарии рядом с кодом;
- абсолютные пути заменены на локальную папку `data/<scenario>/...` внутри output directory.

Запуск:
- `dotnet run -- list`
- `dotnet run -- main10`
- `dotnet run -- main11`
- `dotnet run -- main13`
- `dotnet run -- main14`
- `dotnet run -- main16`
- `dotnet run -- main19`
- `dotnet run -- main20`

Замечания:
- сценарии местами тяжёлые и могут создавать большие файлы;
- объёмы данных и логика intentionally не урезались, чтобы не искажать исходные примеры;
- `TripleStore_mag.cs` оставлен как отдельный исходный файл-хелпер, потому что на него опирается `Main11`.
