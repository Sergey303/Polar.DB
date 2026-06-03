namespace GetStarted.IndexesAndSearch;

internal abstract class ScenarioBase : ISampleScenario
{
    protected ScenarioBase(string id, string title, string sourcePath, bool isExtractedFragment = false)
    {
        Id = id;
        Title = title;
        SourcePath = sourcePath;
        IsExtractedFragment = isExtractedFragment;
    }

    public string Id { get; }
    public string Title { get; }
    public string SourcePath { get; }
    public bool IsExtractedFragment { get; }

    public abstract void Run();

    protected static void PrintHeader(string text)
    {
        Console.WriteLine();
        Console.WriteLine(text);
        Console.WriteLine(new string('-', text.Length));
    }
}
