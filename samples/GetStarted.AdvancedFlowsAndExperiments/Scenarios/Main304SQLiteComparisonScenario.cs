namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Main304SQLiteComparisonScenario : ISampleScenario
{
    public string Id => "gs3-m304";
    public string Title => "Main304SQLite: SQLite comparison experiment";
    public string SourcePath => "samples/GetStarted3/Main304SQLite.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(GetStarted3.Program.datadirectory_path);
        GetStarted3.Program.Main304SQLite();
    }
}
