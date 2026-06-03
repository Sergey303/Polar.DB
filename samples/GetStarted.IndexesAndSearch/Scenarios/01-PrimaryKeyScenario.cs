namespace GetStarted.IndexesAndSearch.Scenarios;

internal sealed class PrimaryKeyScenario : ScenarioBase
{
    public PrimaryKeyScenario()
        : base("primary-key", "Primary key lookup with dynamic append", "Scenarios/01-PrimaryKeyScenario.cs")
    {
    }

    public override void Run()
    {
        var workspace = new ScenarioWorkspace(Id);
        var sequence = PersonSequenceFactory.CreatePrimarySequence(workspace);

        try
        {
            sequence.Load(SamplePeople.BaseDataset());
            sequence.Build();
            sequence.Refresh();

            PrintHeader("Lookup in the built primary-key index");
            var found = sequence.GetByKey(4);
            Console.WriteLine(SamplePeople.Describe(found));

            PrintHeader("Append one more record without rebuilding indexes");
            var appended = SamplePeople.AppendedForPrimaryKey();
            sequence.AppendElement(appended);
            Console.WriteLine("Appended:");
            Console.WriteLine($"  {SamplePeople.Describe(appended)}");

            var foundAfterAppend = sequence.GetByKey(6);
            Console.WriteLine();
            Console.WriteLine("Lookup for id=6 now succeeds immediately:");
            Console.WriteLine($"  {SamplePeople.Describe(foundAfterAppend)}");
        }
        finally
        {
            sequence.Close();
        }
    }
}
