using GetStarted.StructuresAndSerialization.Scenarios;

namespace GetStarted.StructuresAndSerialization;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } =
        new ISampleScenario[]
        {
            new FromGetStartedProgramIntroStructuresAndTextSerializationScenario(),
            new FromGetStarted1Demo101IntroStructuresAndTextSerializationScenario(),
            new FromGetStarted2Program201TypesAndSerializationScenario(),
            new FromGetStarted3Main301TypesAndSerializationScenario(),
            new FromGetStarted4Main401TypesAndSerializationScenario(),
            new FromGetStarted5ProgramIntroStructuresAndSerializationScenario(),
        };
}
