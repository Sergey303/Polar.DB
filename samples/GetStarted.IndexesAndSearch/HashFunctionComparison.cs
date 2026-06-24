using Common;

namespace GetStarted.IndexesAndSearch;

internal static class HashFunctionComparison
{
    public static void Run()
    {
        string[] keys = new[]
        {
            "csharp", "dotnet", "sql", "storage", "search", "graph",
            "analytics", "python", "kafka", "backup", "api", "linux"
        };

        HashReport weakLength = Analyze(keys, "length", key => key.Length);
        HashReport weakFirstChar = Analyze(keys, "first char", key => key[0]);
        HashReport stable = Analyze(keys, "stable", StableStringHash);

        if (!(weakLength.Collisions > 0)) throw new InvalidOperationException("Length hash must show visible collisions");
        if (!(weakFirstChar.Collisions > 0)) throw new InvalidOperationException("First-char hash must show visible collisions");
        Check.Equal(0, stable.Collisions, "Stable hash must avoid collisions on this small key set");

        Print(weakLength);
        Print(weakFirstChar);
        Print(stable);

        Console.WriteLine();
        Console.WriteLine("A hash-compatible index still validates the original key after hash lookup.");
        Console.WriteLine("A better hash only narrows the candidate range and reduces extra comparisons.");
    }

    private static HashReport Analyze(string[] keys, string name, Func<string, int> hash)
    {
        var groups = keys
            .GroupBy(hash)
            .OrderBy(group => group.Key)
            .Select(group => new HashBucket(group.Key, group.OrderBy(key => key).ToArray()))
            .ToArray();

        int collisions = groups.Sum(group => Math.Max(0, group.Keys.Length - 1));
        int maxBucket = groups.Max(group => group.Keys.Length);
        return new HashReport(name, groups.Length, collisions, maxBucket, groups);
    }

    private static int StableStringHash(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (char ch in text)
                hash = hash * 31 + ch;
            return hash;
        }
    }

    private static void Print(HashReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"{report.Name} hash:");
        Console.WriteLine($"  buckets={report.Buckets}, collisions={report.Collisions}, maxBucket={report.MaxBucket}");

        foreach (HashBucket bucket in report.Groups.Where(group => group.Keys.Length > 1))
            Console.WriteLine($"  collision {bucket.Hash}: {string.Join(", ", bucket.Keys)}");
    }

    private sealed record HashReport(
        string Name,
        int Buckets,
        int Collisions,
        int MaxBucket,
        HashBucket[] Groups);

    private sealed record HashBucket(int Hash, string[] Keys);
}
