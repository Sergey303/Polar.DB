namespace GetStarted.PagedStorage;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } = new ISampleScenario[]
    {
        new GetStarted.PagedStorage.Scenarios.Program4TableAndUniversalIndexPagedStorageScenario(),
        new GetStarted.PagedStorage.Scenarios.Program5ThreeSequencesPagedFileStoreScenario(),
        new GetStarted.PagedStorage.Scenarios.Program6StringIdHalfKeyPagedStorageScenario(),
        new GetStarted.PagedStorage.Scenarios.Program7KeyValueStoragePagedStreamsScenario(),
        new GetStarted.PagedStorage.Scenarios.Program8AdvancedPagedKeyValueScenario(),
        new GetStarted.PagedStorage.Scenarios.Program9PagedStorageBenchmarksScenario()
    };
}
