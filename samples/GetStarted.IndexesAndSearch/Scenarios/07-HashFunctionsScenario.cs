using Polar.DB;

namespace GetStarted.IndexesAndSearch;

internal sealed class HashFunctionsScenario : ScenarioBase
{
    public HashFunctionsScenario()
        : base("hash-functions", "Utility hashes used by index examples", "Scenarios/07-HashFunctionsScenario.cs")
    {
    }

    public override void Run()
    {
        PrintHeader("HashRot13 for Latin words");
        foreach (var word in new[] { "search", "storage", "graph", "csharp" })
        {
            Console.WriteLine($"{word,-10} -> {Hashfunctions.HashRot13(word)}");
        }

        PrintHeader("First4charsRu for Russian words");
        foreach (var word in new[] { "поиск", "граф", "данные", "ёжик" })
        {
            Console.WriteLine($"{word,-10} -> {Hashfunctions.First4charsRu(word)}");
        }
    }
}
