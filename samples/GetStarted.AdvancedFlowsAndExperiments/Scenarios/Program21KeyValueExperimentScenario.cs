namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Program21KeyValueExperimentScenario : ISampleScenario
{
    public string Id => "gs1-p21";
    public string Title => "Program21: KVStorage32 experiment";
    public string SourcePath => "samples/GetStarted1/Program21KeyValueExp.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(Path.Combine(GetStarted.Program.dbpath, "Databases"));
        GetStarted.Program.Main21();
    }
}
