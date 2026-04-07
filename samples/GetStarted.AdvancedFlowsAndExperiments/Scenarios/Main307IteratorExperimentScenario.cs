namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Main307IteratorExperimentScenario : ISampleScenario
{
    public string Id => "gs3-m307";
    public string Title => "Main307: iterator/yield experiment";
    public string SourcePath => "samples/GetStarted3/Main307.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        GetStarted3.Program.Main307();
    }
}
