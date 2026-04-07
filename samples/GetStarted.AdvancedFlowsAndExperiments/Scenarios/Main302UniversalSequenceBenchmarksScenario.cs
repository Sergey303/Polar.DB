namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Main302UniversalSequenceBenchmarksScenario : ISampleScenario
{
    public string Id => "gs3-m302";
    public string Title => "Main302: UniversalSequence scan and object/tuple benchmarks";
    public string SourcePath => "samples/GetStarted3/Main302.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(GetStarted3.Program.datadirectory_path);
        GetStarted3.Program.Main302();
    }
}
