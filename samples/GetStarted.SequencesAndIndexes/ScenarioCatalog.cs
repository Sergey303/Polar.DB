using GetStarted.SequencesAndIndexes.Scenarios;

namespace GetStarted.SequencesAndIndexes;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } =
        new ISampleScenario[]
        {
            new FromGetStartedProgramUniversalSequenceAndUSequenceScenario(),
            new FromGetStarted1Demo101UniversalSequenceScenario(),
            new FromGetStarted3Main303OffsetsArrayBinarySearchScenario(),
            new FromGetStarted3Main305FirstBinarySearchScenario(),
            new FromGetStarted3Main306PersistentKeysAndOffsetsScenario(),
        };
}
