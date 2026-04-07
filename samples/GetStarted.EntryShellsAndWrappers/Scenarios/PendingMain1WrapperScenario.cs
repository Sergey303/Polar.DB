using GetStarted.EntryShellsAndWrappers.Abstractions;

namespace GetStarted.EntryShellsAndWrappers.Scenarios;

internal sealed class PendingMain1WrapperScenario : ISampleScenario
{
    public string Id => "pending-main1";
    public string Title => "Неперенесённый сценарий Main1 из GetStarted1";

    public void Run()
    {
        Console.WriteLine("Этот сценарий ещё не был чисто разложен по тематическим архивам.");
        Console.WriteLine();
        Console.WriteLine("Источник:");
        Console.WriteLine("  samples/GetStarted1/Program.cs -> Main1()");
        Console.WriteLine();
        Console.WriteLine("Что в нём происходит:");
        Console.WriteLine("  - создаётся тип записи из трёх полей;");
        Console.WriteLine("  - тип интерпретируется в текст;");
        Console.WriteLine("  - создаётся PaCell на MemoryStream;");
        Console.WriteLine("  - ячейка заполняется структурным значением;");
        Console.WriteLine("  - читается поле записи.");
        Console.WriteLine();
        Console.WriteLine("Извлечённый исходник сохранён в PendingMigration/GetStarted1.Main1.extracted.cs.txt");
        Console.WriteLine("Его логичнее всего потом включить в GetStarted.StructuresAndSerialization.");
    }
}
