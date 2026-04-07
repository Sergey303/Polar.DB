namespace GetStarted.IndexesAndSearch.Scenarios;

internal sealed class AgeIndexScenario : ScenarioBase
{
    private static readonly Comparer<object> AgeComparer = Comparer<object>.Create(
        (left, right) => SamplePeople.Age(left).CompareTo(SamplePeople.Age(right)));

    public AgeIndexScenario()
        : base("age-index", "Secondary UIndex grouped by age", "Scenarios/02-AgeIndexScenario.cs")
    {
    }

    public override void Run()
    {
        var workspace = new ScenarioWorkspace(Id);
        var sequence = PersonSequenceFactory.CreatePrimarySequence(workspace);

        try
        {
            var ageIndex = new UIndex(
                workspace.CreateStreamFactory("age"),
                sequence,
                applicable: _ => true,
                hashFunc: record => SamplePeople.Age(record),
                comp: AgeComparer);

            sequence.uindexes = new IUIndex[] { ageIndex };
            sequence.Load(SamplePeople.BaseDataset());
            sequence.Build();
            sequence.Refresh();

            PrintHeader("Find all records where age = 30");
            var byAge = sequence.GetAllBySample(0, SamplePeople.AgeSample(30)).ToArray();
            ScenarioPrinter.PrintRecords("Initial matches:", byAge);

            PrintHeader("Append another age = 30 record without rebuilding the index");
            var appended = SamplePeople.AppendedForAge();
            sequence.AppendElement(appended);

            var afterAppend = sequence.GetAllBySample(0, SamplePeople.AgeSample(30)).ToArray();
            ScenarioPrinter.PrintRecords("Matches after append:", afterAppend);
        }
        finally
        {
            sequence.Close();
        }
    }
}
