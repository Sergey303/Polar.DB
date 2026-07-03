using Common;
using Polar.DB.ExternalKey;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class ExternalKeySearch
{
    public static void Run()
    {
        string dbPath = DbPath.Create();
        using var sequence = CreateSequence(dbPath);

        var cityIndex = new ExternalKeyIndex<string>(
            streamGen: CreateStreamFactory(dbPath, "city"),
            sequence: sequence,
            keysFunc: CityKeys,
            comparer: StringComparer.Ordinal);

        sequence.uindexes = new IUIndex[] { cityIndex };
        sequence.Load(ExtendPeopleScheme.BaseDataset());
        sequence.Build();
        sequence.Refresh();

        object[] berlinBeforeAppend = FindByCity(sequence, "Berlin");
        Check.SequenceEqual(new[] { 1, 3 }, OrderedIds(berlinBeforeAppend),
            "External city key must find initial Berlin records");

        Console.WriteLine("Lookup by external city key 'Berlin':");
        Print(berlinBeforeAppend);

        object[] riga = FindByCity(sequence, "Riga");
        Check.SequenceEqual(new[] { 4 }, OrderedIds(riga),
            "External city key must find Riga record");

        Console.WriteLine();
        Console.WriteLine("Lookup by another external city key 'Riga':");
        Print(riga);

        sequence.AppendElement(ExtendPeopleScheme.AppendedForPrimaryKey());
        object[] berlinAfterAppend = FindByCity(sequence, "Berlin");
        Check.SequenceEqual(new[] { 1, 3, 6 }, OrderedIds(berlinAfterAppend),
            "External city key must include appended Berlin record without rebuild");

        Console.WriteLine();
        Console.WriteLine("Lookup after append without rebuilding external key index:");
        Print(berlinAfterAppend);
    }

    private static IEnumerable<string> CityKeys(object record)
    {
        yield return ExtendPeopleScheme.City(record);
    }

    private static IEnumerable<IComparable> CityComparables(object record)
    {
        yield return ExtendPeopleScheme.City(record);
    }

    private static object[] FindByCity(USequence sequence, string city)
    {
        return sequence.GetAllByValue(0, city, CityComparables).ToArray();
    }

    private static int[] OrderedIds(IEnumerable<object> records)
    {
        return records.Select(ExtendPeopleScheme.Id).OrderBy(id => id).ToArray();
    }

    private static USequence CreateSequence(string dbPath)
    {
        var nextStreamIndex = 0;
        return new USequence(
            ExtendPeopleScheme.RecordType,
            stateFileName: null,
            streamGen: () => OpenStream(dbPath, $"primary-{nextStreamIndex++:00}.bin"),
            isEmpty: _ => false,
            keyFunc: record => ExtendPeopleScheme.Id(record),
            hashOfKey: key => Convert.ToInt32(key),
            optimise: true);
    }

    private static Func<Stream> CreateStreamFactory(string dbPath, string prefix)
    {
        var nextStreamIndex = 0;
        return () => OpenStream(dbPath, $"{prefix}-{nextStreamIndex++:00}.bin");
    }

    private static FileStream OpenStream(string dbPath, string fileName)
    {
        string path = Path.Combine(dbPath, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }

    private static void Print(IEnumerable<object> records)
    {
        foreach (object record in records.OrderBy(ExtendPeopleScheme.Id))
            Console.WriteLine($"  {ExtendPeopleScheme.Describe(record)}");
    }
}
