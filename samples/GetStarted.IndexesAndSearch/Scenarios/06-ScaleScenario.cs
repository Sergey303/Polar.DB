using Polar.DB;

namespace GetStarted.IndexesAndSearch;

internal sealed class ScaleScenario : ScenarioBase
{
    public ScaleScenario()
        : base("scale", "Scale + Diapason for approximate search windows", "Scenarios/06-ScaleScenario.cs")
    {
    }

    public override void Run()
    {
        var workspace = new ScenarioWorkspace(Id);
        var sortedScores = new[]
        {
            10, 12, 12, 15, 18, 20, 21, 23, 24, 25, 27, 29, 31, 34, 35, 37,
            40, 42, 45, 47, 48, 50, 53, 54, 58, 60, 63, 65, 66, 70, 73, 75
        };

        using var stream = workspace.OpenStream("scale.bin");
        var scale = new Scale(stream);
        try
        {
            scale.Load(sortedScores);

            PrintHeader("Approximate windows for a few probe values");
            PrintWindow(scale.GetDia(24), 24, sortedScores);
            PrintWindow(scale.GetDia(48), 48, sortedScores);
            PrintWindow(scale.GetDia(74), 74, sortedScores);

            PrintHeader("The same idea using the static helper");
            var getDia = Scale.GetDiaFunc32(sortedScores);
            PrintWindow(getDia(31), 31, sortedScores);
        }
        finally
        {
            scale.Close();
        }
    }

    private static void PrintWindow(Diapason diapason, int probe, int[] sortedScores)
    {
        if (diapason.IsEmpty())
        {
            Console.WriteLine($"probe={probe}: <empty diapason>");
            return;
        }

        var values = sortedScores.Skip((int)diapason.start).Take((int)diapason.numb).ToArray();
        Console.WriteLine($"probe={probe}: start={diapason.start}, count={diapason.numb}, values=[{string.Join(", ", values)}]");
    }
}
