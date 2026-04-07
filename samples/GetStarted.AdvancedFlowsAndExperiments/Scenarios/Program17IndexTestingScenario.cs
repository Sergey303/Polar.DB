namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Program17IndexTestingScenario : ISampleScenario
{
    public string Id => "gs1-p17";
    public string Title => "Program17: testing indexes and name views";
    public string SourcePath => "samples/GetStarted1/Program17.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(Path.Combine(GetStarted.Program.dbpath, "Databases"));
        GetStarted.Program.Main17();
    }
}
