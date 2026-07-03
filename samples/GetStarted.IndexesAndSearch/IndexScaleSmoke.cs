using Common;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class IndexScaleSmoke
{
    private const int Count = 200;

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
        sequence.Load(CreatePeople(Count));
        sequence.Build();
        sequence.Refresh();

        object byPrimaryKey = sequence.GetByKey(150);
        Check.Equal(150, ExtendPeopleScheme.Id(byPrimaryKey), "Primary key lookup must find id=150");

        object[] age42 = FindByAge(sequence, 42);
        Check.SequenceEqual(new[] { 22, 62, 102, 142, 182 }, OrderedIds(age42),
            "Age index must find all generated records with age=42");

        Console.WriteLine($"Loaded generated records: {Count}");
        Console.WriteLine("Primary key lookup id=150:");
        Console.WriteLine($"  {ExtendPeopleScheme.Describe(byPrimaryKey)}");
        Console.WriteLine();
        Console.WriteLine("Secondary age index lookup age=42:");
        Print(age42);
    }

    private static IReadOnlyList<object> CreatePeople(int count)
    {
        return Enumerable.Range(1, count)
            .Select(id => (object)ExtendPeopleScheme.Person(
                id,
                FullName(id),
                City(id),
                Age(id),
                Tags(id),
                Skills(id),
                $"Демонстрационная запись #{id} для проверки индексов."))
            .ToArray();
    }

    private static object[] FindByAge(USequence sequence, int age)
    {
        return sequence.GetAllBySample(0, ExtendPeopleScheme.AgeSample(age)).ToArray();
    }

    private static int[] OrderedIds(IEnumerable<object> records)
    {
        return records.Select(ExtendPeopleScheme.Id).OrderBy(id => id).ToArray();
    }

    private static int CompareByAge(object left, object right)
    {
        return ExtendPeopleScheme.Age(left).CompareTo(ExtendPeopleScheme.Age(right));
    }

    private static int Age(int id) => 20 + id % 40;

    private static string FullName(int id)
    {
        string[] names = { "Алексей", "Борис", "Виктор", "Георгий", "Дмитрий", "Егор", "Иван", "Михаил" };
        string[] surnames = { "Иванов", "Петров", "Сидоров", "Смирнов", "Кузнецов", "Попов", "Соколов", "Орлов" };
        return $"{names[id % names.Length]} {surnames[(id / names.Length) % surnames.Length]}";
    }

    private static string City(int id)
    {
        string[] cities = { "Москва", "Новосибирск", "Казань", "Томск" };
        return cities[id % cities.Length];
    }

    private static string[] Tags(int id)
    {
        return id % 2 == 0 ? new[] { "db", "storage" } : new[] { "graph", "search" };
    }

    private static string[] Skills(int id)
    {
        return id % 3 == 0 ? new[] { "csharp", "sql" } : new[] { "dotnet", "analytics" };
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
