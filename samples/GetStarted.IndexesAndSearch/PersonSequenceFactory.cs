using Polar.DB;

namespace GetStarted.IndexesAndSearch;

internal static class PersonSequenceFactory
{
    public static USequence CreatePrimarySequence(ScenarioWorkspace workspace)
    {
        return new USequence(
            SamplePeople.RecordType,
            stateFileName: null,
            streamGen: workspace.CreateStreamFactory("primary"),
            isEmpty: _ => false,
            keyFunc: record => SamplePeople.Id(record),
            hashOfKey: key => Convert.ToInt32(key),
            optimise: true);
    }
}
