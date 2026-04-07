namespace GetStarted.IndexesAndSearch.Scenarios;

internal sealed class TextSearchScenario : ScenarioBase
{
    public TextSearchScenario()
        : base("text-search", "SVectorIndex for exact token and prefix search", "Scenarios/03-TextSearchScenario.cs")
    {
    }

    public override void Run()
    {
        var workspace = new ScenarioWorkspace(Id);
        var sequence = PersonSequenceFactory.CreatePrimarySequence(workspace);

        try
        {
            var textIndex = new SVectorIndex(
                workspace.CreateStreamFactory("text"),
                sequence,
                valuesFunc: SamplePeople.SearchTokens,
                ignorecase: true);

            sequence.uindexes = new IUIndex[] { textIndex };
            sequence.Load(SamplePeople.BaseDataset());
            sequence.Build();
            sequence.Refresh();

            PrintHeader("Exact token search for 'graph'");
            var exact = SamplePeople.DistinctById(
                sequence.GetAllByValue(0, "graph", _ => Array.Empty<IComparable>())).ToArray();
            ScenarioPrinter.PrintRecords("Matches:", exact);

            PrintHeader("Prefix search for 'ana'");
            var prefix = SamplePeople.DistinctById(sequence.GetAllByLike(0, "ana")).ToArray();
            ScenarioPrinter.PrintRecords("Matches:", prefix);

            PrintHeader("Append a new record and search again without rebuild");
            sequence.AppendElement(SamplePeople.AppendedForTextSearch());
            var afterAppend = SamplePeople.DistinctById(sequence.GetAllByLike(0, "ana")).ToArray();
            ScenarioPrinter.PrintRecords("Matches after append:", afterAppend);
        }
        finally
        {
            sequence.Close();
        }
    }
}
