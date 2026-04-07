namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Program12UniversalSequenceIndexExperimentScenario : ISampleScenario
{
    public string Id => "gs1-p12";
    public string Title => "Program12: UniversalSequence + manual key/offset index";
    public string SourcePath => "samples/GetStarted1/Program12.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(Path.Combine(SamplePaths.Combine("Program12"), "Databases"));
        GetStarted.Program.Main12();
    }
}
