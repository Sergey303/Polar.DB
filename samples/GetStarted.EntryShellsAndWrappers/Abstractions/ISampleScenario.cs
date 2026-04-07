namespace GetStarted.EntryShellsAndWrappers.Abstractions;

internal interface ISampleScenario
{
    string Id { get; }
    string Title { get; }
    void Run();
}
