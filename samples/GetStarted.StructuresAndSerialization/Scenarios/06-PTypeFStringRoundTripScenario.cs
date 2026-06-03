using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class PTypeFStringRoundTripScenario : ISampleScenario
{
    public string Id => "fstring";
    public string Title => "PTypeFString schema round-trip";
    public string SourcePath => "Scenarios/06-PTypeFStringRoundTripScenario.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        var original = new PTypeFString(16);
        var pObject = original.ToPObject(8);
        var restored = PType.FromPObject(pObject);

        Console.WriteLine("Original schema:");
        Console.WriteLine(PType.TType.Interpret(original.ToPObject(8)));
        Console.WriteLine();
        Console.WriteLine("Restored schema:");
        Console.WriteLine(PType.TType.Interpret(restored.ToPObject(8)));
        Console.WriteLine();
        Console.WriteLine($"Length={((PTypeFString)restored).Length}, HeadSize={restored.HeadSize}");
    }
}
