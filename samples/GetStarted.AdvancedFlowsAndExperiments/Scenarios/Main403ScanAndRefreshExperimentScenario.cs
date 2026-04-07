namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Main403ScanAndRefreshExperimentScenario : ISampleScenario
{
    public string Id => "gs4-m403";
    public string Title => "Main403: scan/refresh with sorted offsets";
    public string SourcePath => "samples/GetStarted4/Main403.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(GetStarted4.Program.datadirectory_path);
        GetStarted4.Program.Main403();
    }
}
