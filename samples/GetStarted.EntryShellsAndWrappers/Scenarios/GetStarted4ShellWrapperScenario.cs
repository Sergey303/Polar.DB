using GetStarted.EntryShellsAndWrappers.Abstractions;

namespace GetStarted.EntryShellsAndWrappers.Scenarios;

internal sealed class GetStarted4ShellWrapperScenario : ISampleScenario
{
    public string Id => "gs4-shell";
    public string Title => "Старый entry-shell проекта GetStarted4";

    public void Run()
    {
        Console.WriteLine("Исходный shell:");
        Console.WriteLine("  samples/GetStarted4/Program.cs");
        Console.WriteLine();
        Console.WriteLine("Что делал shell:");
        Console.WriteLine("  - вручную переключал Main400 / Main401 / Main402 / Main403 / Main404flows;");
        Console.WriteLine("  - держал общие datadirectory_path и Stopwatch.");
        Console.WriteLine();
        Console.WriteLine("В новой структуре сценарии уже разложены по:");
        Console.WriteLine("  - GetStarted.StructuresAndSerialization");
        Console.WriteLine("  - GetStarted.AdvancedFlowsAndExperiments");
        Console.WriteLine();
        Console.WriteLine("Оригинал сохранён в LegacyEntryShells/GetStarted4.Program.cs.txt");
    }
}
