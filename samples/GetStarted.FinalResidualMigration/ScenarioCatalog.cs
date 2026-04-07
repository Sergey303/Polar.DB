using GetStarted.FinalResidualMigration.Scenarios;

namespace GetStarted.FinalResidualMigration;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } =
        new ISampleScenario[]
        {
            new HistoricalShellsSummaryScenario()
        };
}
