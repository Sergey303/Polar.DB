using System.Text;

namespace PolarDbBenchmarks;

public enum LookupKind
{
    PrimaryInt,
    PrimaryString,
    ExternalInt,
    ExternalString
}

public sealed record LookupOptions(
    string ExperimentId,
    string Title,
    LookupKind Kind,
    int SetupRows,
    int WarmupOps,
    int MeasuredOps);

internal sealed record Row(
    long Id,
    string SKey,
    int ExternalId,
    string ExternalKey,
    string Payload);

internal sealed record QueryResult(long Rows, ulong Checksum);

internal sealed record EngineResult(
    string Engine,
    string Status,
    IReadOnlyList<double> SamplesMs,
    long Rows,
    ulong Checksum,
    long ArtifactBytes);

public static class LookupBench
{
    public static void Run(LookupOptions options)
    {
        var repo = FindRepoRoot();
        var work = Path.Combine(repo, "benchmarks", "work", options.ExperimentId);
        var results = Path.Combine(repo, "benchmarks", "results");

        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(results);

        var data = Dataset(options.SetupRows).ToArray();
        var engines = new[]
        {
            LookupEngines.RunSqlite(options, data, Path.Combine(work, "sqlite")),
            LookupEngines.RunPolarDb(options, data, Path.Combine(work, "polar"))
        };

        var output = Path.Combine(results, options.ExperimentId + ".html");
        File.WriteAllText(output, LookupReport.Render(options, engines), Encoding.UTF8);
        Console.WriteLine(output);
    }

    internal static IEnumerable<object> LookupKeys(Row[] rows, LookupKind kind, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var r = rows[i % rows.Length];
            yield return kind switch
            {
                LookupKind.PrimaryInt => r.Id,
                LookupKind.PrimaryString => r.SKey,
                LookupKind.ExternalInt => r.ExternalId,
                LookupKind.ExternalString => r.ExternalKey,
                _ => r.Id
            };
        }
    }

    internal static ulong Hash(Row row)
    {
        unchecked
        {
            ulong h = 14695981039346656037UL;
            void Add(string s)
            {
                foreach (var ch in s)
                {
                    h ^= ch;
                    h *= 1099511628211UL;
                }
            }

            h ^= (ulong)row.Id;
            h *= 1099511628211UL;
            h ^= (ulong)row.ExternalId;
            h *= 1099511628211UL;
            Add(row.SKey);
            Add(row.ExternalKey);
            Add(row.Payload);
            return h;
        }
    }

    internal static long DirBytes(string dir) => Directory.Exists(dir)
        ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length)
        : 0L;

    private static IEnumerable<Row> Dataset(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            yield return new Row(
                i,
                $"id-{i:000000000}",
                i % 1000,
                $"group-{i % 1000:0000}",
                $"payload-{i:000000000}");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir != null)
        {
            var project = Path.Combine(dir.FullName, "src", "Polar.DB", "Polar.DB.csproj");
            if (File.Exists(project)) return dir.FullName;
            dir = dir.Parent;
        }

        return Environment.CurrentDirectory;
    }
}
