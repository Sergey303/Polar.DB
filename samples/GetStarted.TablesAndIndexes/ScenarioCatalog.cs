using GetStarted.TablesAndIndexes.Scenarios;

namespace GetStarted.TablesAndIndexes;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } =
        new ISampleScenario[]
        {
            new Program2TableAndSimpleIndexesScenario(),
            new Program3TableAndUniversalIndexScenario()
        };
}
