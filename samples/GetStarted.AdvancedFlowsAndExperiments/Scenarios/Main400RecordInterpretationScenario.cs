namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Main400RecordInterpretationScenario : ISampleScenario
{
    public string Id => "gs4-m400";
    public string Title => "Main400: record interpretation demo";
    public string SourcePath => "samples/GetStarted4/Main400.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        GetStarted4.Program.Main400();
    }
}
