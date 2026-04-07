namespace GetStarted.AdvancedFlowsAndExperiments.Scenarios;

internal sealed class TestSerializationScenario : ISampleScenario
{
    public string Id => "test";
    public string Title => "Test: union + text/binary serialization";
    public string SourcePath => "samples/GetStarted1/Test.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        SamplePaths.EnsureDirectory(GetStarted.Program.dbpath);
        GetStarted.Program.Test();
    }
}
