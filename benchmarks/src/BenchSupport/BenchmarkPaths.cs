namespace PolarDbBenchmarks;

internal static class BenchmarkPaths
{
    public static string PrepareWorkDir(string experimentId)
    {
        var root = FindRepoRoot();
        var work = Path.Combine(root, "benchmarks", "work", experimentId);
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(Path.Combine(root, "benchmarks", "results"));
        return work;
    }

    public static string ResultPath(string experimentId)
    {
        var root = FindRepoRoot();
        return Path.Combine(root, "benchmarks", "results", experimentId + ".html");
    }

    public static long DirBytes(string dir)
    {
        if (!Directory.Exists(dir)) return 0L;
        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
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
