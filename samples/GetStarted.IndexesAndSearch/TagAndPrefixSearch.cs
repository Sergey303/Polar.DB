using Common;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class TagAndPrefixSearch
{
    public static void Run()
    {
        string dbPath = DbPath.Create();
        using var sequence = CreateSequence(dbPath);

        var tagIndex = new SVectorIndex(
            streamGen: CreateStreamFactory(dbPath, "tags"),
            sequence: sequence,
            valuesFunc: SamplePeople.Tags,
            ignorecase: false);

        sequence.uindexes = new IUIndex[] { tagIndex };
        sequence.Load(SamplePeople.BaseDataset());
        sequence.Build();
        sequence.Refresh();

        var storageBeforeAppend = FindByTag(sequence, "storage");
        Check.SequenceEqual(new[] { 2, 5 }, storageBeforeAppend.Select(SamplePeople.Id).OrderBy(id => id),
            "Exact tag lookup must find initial storage records");

        Console.WriteLine("Exact lookup by tag 'storage':");
        Print(storageBeforeAppend);

        sequence.AppendElement(SamplePeople.AppendedForTagSearch());
        var storageAfterAppend = FindByTag(sequence, "storage");
        Check.SequenceEqual(new[] { 2, 5, 9 }, storageAfterAppend.Select(SamplePeople.Id).OrderBy(id => id),
            "Exact tag lookup must include appended storage record without rebuild");

        Console.WriteLine();
        Console.WriteLine("Exact lookup after append without rebuilding indexes:");
        Print(storageAfterAppend);

        sequence.AppendElement(SamplePeople.AppendedForTextSearch());
        var graphPrefix = sequence.GetAllByLike(0, "gra").ToArray();
        Check.SequenceEqual(new[] { 3, 4, 8 }, graphPrefix.Select(SamplePeople.Id).OrderBy(id => id),
            "Prefix lookup by 'gra' must find graph-tagged records, including appended record");

        Console.WriteLine();
        Console.WriteLine("Prefix lookup by tag prefix 'gra':");
        Print(graphPrefix);
    }

    private static object[] FindByTag(USequence sequence, string tag)
    {
        return sequence.GetAllByValue(0, tag, SamplePeople.TagsAsComparables).ToArray();
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
        foreach (object record in records.OrderBy(SamplePeople.Id))
            Console.WriteLine($"  {SamplePeople.Describe(record)}");
    }
}
