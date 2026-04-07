namespace GetStarted.AdvancedFlowsAndExperiments;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISampleScenario> All { get; } = new ISampleScenario[]
    {
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.TestSerializationScenario(),
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.Main302UniversalSequenceBenchmarksScenario(),
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.Main304SQLiteComparisonScenario(),
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.Main307IteratorExperimentScenario(),
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.Main400RecordInterpretationScenario(),
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.Main402BinarySearchOverOffsetsScenario(),
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.Main403ScanAndRefreshExperimentScenario(),
        new GetStarted.AdvancedFlowsAndExperiments.Scenarios.Main404FlowPrototypeScenario(),
    };
}
