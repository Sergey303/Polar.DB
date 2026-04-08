using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class RecordAccessStylesScenario : ISampleScenario
{
    public string Id => "record-access-styles";
    public string Title => "Object-like and RecordAccessor-like parity";
    public string SourcePath => "Scenarios/09-RecordAccessStylesScenario.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        var personType = SamplePersonSchema.PersonType;
        var accessor = new RecordAccessor(personType);

        // Object-like
        var objectLike = new object[] { 1, "Alice", 30 };

        // RecordAccessor-like
        var accessorLike = accessor.CreateRecord(1, "Alice", 30);

        Console.WriteLine($"Object-like: id={objectLike[0]}, name={objectLike[1]}, age={objectLike[2]}");
        Console.WriteLine($"RecordAccessor-like: id={accessor.Get<int>(accessorLike, "id")}, name={accessor.Get<string>(accessorLike, "name")}, age={accessor.Get<int>(accessorLike, "age")}");
    }
}
