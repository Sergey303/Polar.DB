namespace GetStarted.PagedStorage;

internal interface ISampleScenario
{
    string Id { get; }
    string Title { get; }
    string SourcePath { get; }
    bool IsExtractedFragment { get; }
    void Run();
}
