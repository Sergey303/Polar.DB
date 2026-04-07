using GetStarted.EntryShellsAndWrappers.Abstractions;

namespace GetStarted.EntryShellsAndWrappers.Scenarios;

internal sealed class GetStarted3ShellWrapperScenario : ISampleScenario
{
    public string Id => "gs3-shell";
    public string Title => "Старый entry-shell проекта GetStarted3";

    public void Run()
    {
        Console.WriteLine("Исходный shell:");
        Console.WriteLine("  samples/GetStarted3/Program.cs");
        Console.WriteLine();
        Console.WriteLine("Что делал shell:");
        Console.WriteLine("  - задавал общую datadirectory_path;");
        Console.WriteLine("  - держал Stopwatch;");
        Console.WriteLine("  - вручную переключал Main301 / Main302 / Main303 / Main305 / Main306 / Main307 / Main304SQLite.");
        Console.WriteLine();
        Console.WriteLine("В новой структуре сценарии уже разложены по:");
        Console.WriteLine("  - GetStarted.StructuresAndSerialization");
        Console.WriteLine("  - GetStarted.SequencesAndIndexes");
        Console.WriteLine("  - GetStarted.AdvancedFlowsAndExperiments");
        Console.WriteLine();
        Console.WriteLine("Оригинал сохранён в LegacyEntryShells/GetStarted3.Program.cs.txt");
    }
}
