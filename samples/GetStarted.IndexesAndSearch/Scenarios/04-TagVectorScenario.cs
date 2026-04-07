using Polar.DB;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal sealed class TagVectorScenario : ScenarioBase
{
    public TagVectorScenario()
        : base("tag-vector", "UVectorIndex for exact multi-value tag lookup", "Scenarios/04-TagVectorScenario.cs")
    {
    }

    public override void Run()
    {
        var workspace = new ScenarioWorkspace(Id);
        var sequence = PersonSequenceFactory.CreatePrimarySequence(workspace);

        try
        {
            var tagIndex = new UVectorIndex(
                workspace.CreateStreamFactory("tags"),
                sequence,
                tp_value: new PType(PTypeEnumeration.sstring),
                valuesFunc: SamplePeople.TagsAsComparables);

            sequence.uindexes = new IUIndex[] { tagIndex };
            sequence.Load(SamplePeople.BaseDataset());
            sequence.Build();
            sequence.Refresh();

            PrintHeader("Find all records tagged with 'storage'");
            var storageRecords = sequence.GetAllByValue(0, "storage", SamplePeople.TagsAsComparables).ToArray();
            ScenarioPrinter.PrintRecords("Matches:", storageRecords);

            PrintHeader("Append a new 'storage' record without rebuild");
            sequence.AppendElement(SamplePeople.AppendedForTagSearch());
            var afterAppend = sequence.GetAllByValue(0, "storage", SamplePeople.TagsAsComparables).ToArray();
            ScenarioPrinter.PrintRecords("Matches after append:", afterAppend);
        }
        finally
        {
            sequence.Close();
        }
    }
}
