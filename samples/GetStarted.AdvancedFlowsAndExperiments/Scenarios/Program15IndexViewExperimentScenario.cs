namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Program15IndexViewExperimentScenario : ISampleScenario
{
    public string Id => "gs1-p15";
    public string Title => "Program15: IndexKey32CompImm + IndexViewImmutable experiment";
    public string SourcePath => "samples/GetStarted1/Program15.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(Path.Combine(SamplePaths.Combine("Program15"), "Databases"));
        GetStarted.Program.Main15();
    }
}
