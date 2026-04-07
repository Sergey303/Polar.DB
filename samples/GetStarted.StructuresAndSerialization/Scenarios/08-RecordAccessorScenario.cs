using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class RecordAccessorScenario : ISampleScenario
{
    public string Id => "record-accessor";
    public string Title => "RecordAccessor named get/set";
    public string SourcePath => "Scenarios/08-RecordAccessorScenario.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        var accessor = new RecordAccessor(SamplePersonSchema.PersonType);
        object record = accessor.CreateRecord(1, "Alice", 30);

        Console.WriteLine("Initial record:");
        Console.WriteLine(SamplePersonSchema.PersonType.Interpret(record, withfieldnames: true));

        var currentAge = accessor.Get<int>(record, "age");
        accessor.Set(record, "age", currentAge + 1);
        accessor.Set(record, "name", "Alice Cooper");

        Console.WriteLine();
        Console.WriteLine("After named mutations:");
        Console.WriteLine(SamplePersonSchema.PersonType.Interpret(record, withfieldnames: true));
    }
}
