using Common;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class PrimaryKeyLookup
{
    public static void Run()
    {
        string dbPath = DbPath.Create();
        using var sequence = CreateSequence(dbPath);

        sequence.Load(ExtendPeopleScheme.BaseDataset());
        sequence.Build();
        sequence.Refresh();

        object found = sequence.GetByKey(4);
        Check.Equal("Дмитрий Иванов", ExtendPeopleScheme.Name(found), "Lookup by built primary key must find id=4");

        Console.WriteLine("Lookup in the built primary-key index:");
        Console.WriteLine($"  {ExtendPeopleScheme.Describe(found)}");

        object appended = ExtendPeopleScheme.AppendedForPrimaryKey();
        sequence.AppendElement(appended);

        object foundAfterAppend = sequence.GetByKey(6);
        Check.Equal("Федор Новиков", ExtendPeopleScheme.Name(foundAfterAppend),
            "Lookup by dynamic primary key must find appended id=6 without rebuild");

        Console.WriteLine();
        Console.WriteLine("Lookup after append without rebuilding indexes:");
        Console.WriteLine($"  {ExtendPeopleScheme.Describe(foundAfterAppend)}");

        var liveIds = sequence.ElementValues().Select(ExtendPeopleScheme.Id);
        Check.SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }, liveIds,
            "Primary sequence must enumerate loaded and appended records");
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

    private static FileStream OpenStream(string dbPath, string fileName)
    {
        string path = Path.Combine(dbPath, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }
}
