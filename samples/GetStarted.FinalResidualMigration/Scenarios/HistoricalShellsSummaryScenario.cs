namespace GetStarted.FinalResidualMigration.Scenarios;

internal sealed class HistoricalShellsSummaryScenario : ISampleScenario
{
    public string Id => "shells";
    public string Title => "Справка по старым shell-файлам GetStarted2/3/4";
    public string SourcePath => "samples/GetStarted2/Program.cs + samples/GetStarted3/Program.cs + samples/GetStarted4/Program.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        Console.WriteLine("Эти старые Program.cs не содержат самостоятельной новой предметной логики.");
        Console.WriteLine("Их роль в старой структуре — выбрать, какой Main... запускать.");
        Console.WriteLine();
        Console.WriteLine("GetStarted2/Program.cs");
        Console.WriteLine("  -> вызывает Main201()");
        Console.WriteLine();
        Console.WriteLine("GetStarted3/Program.cs");
        Console.WriteLine("  -> переключает Main302 / Main303 / Main305 / Main306 / Main307 / Main304SQLite");
        Console.WriteLine();
        Console.WriteLine("GetStarted4/Program.cs");
        Console.WriteLine("  -> переключает Main400 / Main401 / Main402 / Main403 / Main404flows");
        Console.WriteLine();
        Console.WriteLine("Поэтому эти shell-файлы сохранены в архиве как historical reference,");
        Console.WriteLine("но не превращены в отдельные новые сценарии, чтобы не плодить пустые оболочки.");
    }
}
