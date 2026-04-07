using Polar.DB;

namespace GetStarted.IndexesAndSearch.Scenarios;

internal sealed class SkillHashScenario : ScenarioBase
{
    public SkillHashScenario()
        : base("skill-hash", "UVecIndex with hash-based multi-value lookup", "Scenarios/05-SkillHashScenario.cs")
    {
    }

    public override void Run()
    {
        var workspace = new ScenarioWorkspace(Id);
        var sequence = PersonSequenceFactory.CreatePrimarySequence(workspace);

        try
        {
            var skillIndex = new UVecIndex(
                workspace.CreateStreamFactory("skills"),
                sequence,
                keysFunc: SamplePeople.SkillsAsComparables,
                hashOfKey: key => Hashfunctions.HashRot13((string)key),
                ignorecase: true);

            sequence.uindexes = new IUIndex[] { skillIndex };
            sequence.Load(SamplePeople.BaseDataset());
            sequence.Build();
            sequence.Refresh();

            PrintHeader("Hash-based lookup for skill 'CSHARP'");
            var csharp = sequence.GetAllByValue(0, "CSHARP", SamplePeople.SkillsAsComparables, ignorecase: true).ToArray();
            ScenarioPrinter.PrintRecords("Matches:", csharp);

            PrintHeader("Append one more CSharp record without rebuild");
            sequence.AppendElement(SamplePeople.AppendedForSkillSearch());
            var afterAppend = sequence.GetAllByValue(0, "CSHARP", SamplePeople.SkillsAsComparables, ignorecase: true).ToArray();
            ScenarioPrinter.PrintRecords("Matches after append:", afterAppend);
        }
        finally
        {
            sequence.Close();
        }
    }
}
