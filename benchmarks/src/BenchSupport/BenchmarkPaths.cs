using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class BenchmarkPaths
{
    public static string PrepareWorkDir(string experimentId)
    {
        var work = WorkDir(experimentId);
        CleanupWorkDir(experimentId);
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(Path.Combine(FindRepoRoot(), "benchmarks", "results"));
        return work;
    }

    public static void CleanupWorkDir(string experimentId)
    {
        DeleteDirectory(WorkDir(experimentId), throwOnFailure: true);
    }

    public static void TryCleanupWorkDir(string experimentId)
    {
        DeleteDirectory(WorkDir(experimentId), throwOnFailure: false);
    }

    public static void CleanupAllWork()
    {
        var workRoot = Path.Combine(FindRepoRoot(), "benchmarks", "work");
        DeleteDirectory(workRoot, throwOnFailure: true);
    }

    public static string ResultPath(string experimentId) =>
        Path.Combine(FindRepoRoot(), "benchmarks", "results", experimentId + ".html");

    public static long DirBytes(string dir)
    {
        if (!Directory.Exists(dir)) return 0L;
        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }

    private static string WorkDir(string experimentId) =>
        Path.Combine(FindRepoRoot(), "benchmarks", "work", experimentId);

    private static void DeleteDirectory(string path, bool throwOnFailure)
    {
        if (!Directory.Exists(path)) return;

        Exception? last = null;
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            ReleaseFileHandles();
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                last = ex;
                Thread.Sleep(150 * attempt);
            }
        }

        if (throwOnFailure && last != null) throw last;
        if (last != null) Console.WriteLine("[bench] cleanup warning: " + last.Message);
    }

    private static void ReleaseFileHandles()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static bool IsRetryable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

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
