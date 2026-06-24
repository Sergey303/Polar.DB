using Common;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class TextTokenSearch
{
    public static void Run()
    {
        string dbPath = DbPath.Create();
        using var sequence = CreateSequence(dbPath);

        var textIndex = new SVectorIndex(
            streamGen: CreateStreamFactory(dbPath, "text"),
            sequence: sequence,
            valuesFunc: Tokens,
            ignorecase: false);

        sequence.uindexes = new IUIndex[] { textIndex };
        sequence.Load(SamplePeople.BaseDataset());
        sequence.Build();
        sequence.Refresh();

        object[] иванов = FindByToken(sequence, "иванов");
        Check.SequenceEqual(new[] { 4 }, OrderedIds(иванов),
            "Text token lookup must find Дмитрий Иванов");

        Console.WriteLine("Exact lookup by text token 'иванов':");
        Print(иванов);

        sequence.AppendElement(SamplePeople.AppendedForPrimaryKey());
        object[] федор = FindByToken(sequence, "федор");
        Check.SequenceEqual(new[] { 6 }, OrderedIds(федор),
            "Text token lookup must include appended Федор without rebuild");

        Console.WriteLine();
        Console.WriteLine("Exact token lookup after append without rebuilding indexes:");
        Print(федор);

        object[] ivPrefix = sequence.GetAllByLike(0, "ива").ToArray();
        Check.SequenceEqual(new[] { 4 }, OrderedIds(ivPrefix),
            "Text prefix lookup by 'ива' must find Иванов");

        Console.WriteLine();
        Console.WriteLine("Prefix lookup by text token prefix 'ива':");
        Print(ivPrefix);
    }

    private static object[] FindByToken(USequence sequence, string token)
    {
        return sequence.GetAllByValue(0, token, TokenComparables).ToArray();
    }

    private static IEnumerable<IComparable> TokenComparables(object record)
    {
        return Tokens(record).Cast<IComparable>();
    }

    private static IEnumerable<string> Tokens(object record)
    {
        foreach (string token in SplitWords(SamplePeople.Name(record)))
            yield return token;

        foreach (string tag in SamplePeople.Tags(record))
            yield return tag.ToLowerInvariant();
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant());
    }

    private static int[] OrderedIds(IEnumerable<object> records)
    {
        return records.Select(SamplePeople.Id).OrderBy(id => id).ToArray();
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
