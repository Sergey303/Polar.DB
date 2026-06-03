using GetStarted.IndexesAndSearch.Scenarios;

namespace GetStarted.IndexesAndSearch;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } =
        new ISampleScenario[]
        {
            new PrimaryKeyScenario(),
            new AgeIndexScenario(),
            new TextSearchScenario(),
            new TagVectorScenario(),
            new SkillHashScenario(),
            new ScaleScenario(),
            new HashFunctionsScenario()
        };
}
