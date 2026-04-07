namespace GetStarted.IndexesAndSearch;

internal static class ScenarioPrinter
{
    public static void PrintRecords(string label, IEnumerable<object> records)
    {
        var list = records.ToList();
        Console.WriteLine(label);
        if (list.Count == 0)
        {
            Console.WriteLine("  <empty>");
            return;
        }

        foreach (var record in list)
        {
            Console.WriteLine($"  {SamplePeople.Describe(record)}");
        }
    }
}
