using Common;
using Polar.Universal;

namespace GetStarted.IndexesAndSearch;

internal static class SkillHashSearch
{
    public static void Run()
    {
        string dbPath = DbPath.Create();
        using var sequence = CreateSequence(dbPath);

        var skillIndex = new UVecIndex(
            streamGen: CreateStreamFactory(dbPath, "skills"),
            sequence: sequence,
            keysFunc: SamplePeople.SkillsAsComparables,
            hashOfKey: StableStringHash,
            ignorecase: false);

        sequence.uindexes = new IUIndex[] { skillIndex };
        sequence.Load(SamplePeople.BaseDataset());
        sequence.Build();
        sequence.Refresh();

        object[] sqlBeforeAppend = FindBySkill(sequence, "sql");
        Check.SequenceEqual(new[] { 1, 5 }, OrderedIds(sqlBeforeAppend),
            "Skill hash index must find initial sql records");

        Console.WriteLine("Lookup by hashed skill 'sql':");
        Print(sqlBeforeAppend);

        sequence.AppendElement(SamplePeople.AppendedForAge());
        object[] sqlAfterAppend = FindBySkill(sequence, "sql");
        Check.SequenceEqual(new[] { 1, 5, 7 }, OrderedIds(sqlAfterAppend),
            "Skill hash index must include appended sql record without rebuild");

        Console.WriteLine();
        Console.WriteLine("Lookup after append without rebuilding skill hash index:");
        Print(sqlAfterAppend);

        object[] dotnet = FindBySkill(sequence, "dotnet");
        Check.SequenceEqual(new[] { 4 }, OrderedIds(dotnet),
            "Skill hash index must find dotnet record");

        Console.WriteLine();
        Console.WriteLine("Lookup by another hashed skill 'dotnet':");
        Print(dotnet);
    }

    private static object[] FindBySkill(USequence sequence, string skill)
    {
        return sequence.GetAllByValue(0, skill, SamplePeople.SkillsAsComparables).ToArray();
    }

    private static int[] OrderedIds(IEnumerable<object> records)
    {
        return records.Select(SamplePeople.Id).OrderBy(id => id).ToArray();
    }

    private static int StableStringHash(IComparable key)
    {
        string text = (string)key;
        unchecked
        {
            int hash = 17;
            foreach (char ch in text)
                hash = hash * 31 + ch;
            return hash;
        }
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
