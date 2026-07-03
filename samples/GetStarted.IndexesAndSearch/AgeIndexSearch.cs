using Common;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class AgeIndexSearch
{
    public static void Run()
    {
        string dbPath = DbPath.Create();
        using var sequence = CreateSequence(dbPath);

        var ageIndex = new UIndex(
            streamGen: CreateStreamFactory(dbPath, "age"),
            sequence: sequence,
            applicable: _ => true,
            hashFunc: ExtendPeopleScheme.Age,
            comp: Comparer<object>.Create(CompareByAge));

        sequence.uindexes = new IUIndex[] { ageIndex };
        sequence.Load(ExtendPeopleScheme.BaseDataset());
        sequence.Build();
        sequence.Refresh();

        object age30Sample = ExtendPeopleScheme.AgeSample(30);
        object[] age30BeforeAppend = FindByAge(sequence, age30Sample);
        Check.SequenceEqual(new[] { 1, 3 }, OrderedIds(age30BeforeAppend),
            "Age index must find initial age=30 records");

        Console.WriteLine("Lookup by secondary age index, age=30:");
        Print(age30BeforeAppend);

        sequence.AppendElement(ExtendPeopleScheme.AppendedForAge());
        object[] age30AfterAppend = FindByAge(sequence, age30Sample);
        Check.SequenceEqual(new[] { 1, 3, 7 }, OrderedIds(age30AfterAppend),
            "Age index must include appended age=30 record without rebuild");

        Console.WriteLine();
        Console.WriteLine("Lookup after append without rebuilding age index:");
        Print(age30AfterAppend);
    }

    private static object[] FindByAge(USequence sequence, object sample)
    {
        return sequence.GetAllBySample(0, sample).ToArray();
    }

    private static int[] OrderedIds(IEnumerable<object> records)
    {
        return records.Select(ExtendPeopleScheme.Id).OrderBy(id => id).ToArray();
    }

    private static int CompareByAge(object left, object right)
    {
        return ExtendPeopleScheme.Age(left).CompareTo(ExtendPeopleScheme.Age(right));
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
            hashOfKey: Convert.ToInt32,
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
