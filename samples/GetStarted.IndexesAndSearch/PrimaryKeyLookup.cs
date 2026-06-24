using Common;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class PrimaryKeyLookup
{
    public static void Run()
    {
        string dbPath = DbPath.Create();
        using var sequence = CreateSequence(dbPath);

        sequence.Load(SamplePeople.BaseDataset());
        sequence.Build();
        sequence.Refresh();

        object found = sequence.GetByKey(4);
        Check.Equal("Дмитрий Иванов", SamplePeople.Name(found), "Lookup by built primary key must find id=4");

        Console.WriteLine("Lookup in the built primary-key index:");
        Console.WriteLine($"  {SamplePeople.Describe(found)}");

        object appended = SamplePeople.AppendedForPrimaryKey();
        sequence.AppendElement(appended);

        object foundAfterAppend = sequence.GetByKey(6);
        Check.Equal("Федор Новиков", SamplePeople.Name(foundAfterAppend),
            "Lookup by dynamic primary key must find appended id=6 without rebuild");

        Console.WriteLine();
        Console.WriteLine("Lookup after append without rebuilding indexes:");
        Console.WriteLine($"  {SamplePeople.Describe(foundAfterAppend)}");

        var liveIds = sequence.ElementValues().Select(SamplePeople.Id);
        Check.SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }, liveIds,
            "Primary sequence must enumerate loaded and appended records");
    }

    private static USequence CreateSequence(string dbPath)
    {
        var nextStreamIndex = 0;
        return new USequence(
            SamplePeople.RecordType,
            stateFileName: null,
            streamGen: () => OpenStream(dbPath, $"primary-{nextStreamIndex++:00}.bin"),
            isEmpty: _ => false,
            keyFunc: record => SamplePeople.Id(record),
            hashOfKey: key => Convert.ToInt32(key),
            optimise: true);
    }

    private static FileStream OpenStream(string dbPath, string fileName)
    {
        string path = Path.Combine(dbPath, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }
}
