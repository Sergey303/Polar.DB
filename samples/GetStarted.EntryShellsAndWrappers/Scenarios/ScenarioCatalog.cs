using GetStarted.EntryShellsAndWrappers.Abstractions;

namespace GetStarted.EntryShellsAndWrappers.Scenarios;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } = new ISampleScenario[]
    {
        new GetStarted1ShellWrapperScenario(),
        new GetStarted2ShellWrapperScenario(),
        new GetStarted3ShellWrapperScenario(),
        new GetStarted4ShellWrapperScenario(),
        new PendingMain1WrapperScenario(),
    };
}
