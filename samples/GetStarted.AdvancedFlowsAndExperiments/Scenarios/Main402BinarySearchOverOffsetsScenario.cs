namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class Main402BinarySearchOverOffsetsScenario : ISampleScenario
{
    public string Id => "gs4-m402";
    public string Title => "Main402: binary search over sorted offsets";
    public string SourcePath => "samples/GetStarted4/Main402.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(GetStarted4.Program.datadirectory_path);
        GetStarted4.Program.Main402();
    }
}
