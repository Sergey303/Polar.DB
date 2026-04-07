namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Main404FlowPrototypeScenario : ISampleScenario
{
    public string Id => "gs4-m404";
    public string Title => "Main404flows: flow prototype with source/sink";
    public string SourcePath => "samples/GetStarted4/Main404flows.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        GetStarted4.Program.Main404flows();
    }
}
