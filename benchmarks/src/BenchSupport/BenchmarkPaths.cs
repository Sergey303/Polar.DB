using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class BenchmarkPaths
{
    public static string RepoRoot => FindRepoRoot();

    public static string PrepareEngineWorkDir(string experimentId, string runId, BenchmarkEngine engine)
    {
        var work = EngineWorkDir(experimentId, runId, engine);
        DeleteDirectory(work, throwOnFailure: true);
        Directory.CreateDirectory(work);
        EnsureResultDirectories(experimentId, runId);
        return work;
    }

    public static void EnsureResultDirectories(string experimentId, string runId)
    {
        Directory.CreateDirectory(ResultsDir());
        Directory.CreateDirectory(RawRunDir(experimentId, runId));
    }

    public static void CleanupAllWork()
    {
        var workRoot = Path.Combine(RepoRoot, "benchmarks", "work");
        DeleteDirectory(workRoot, throwOnFailure: true);
    }

    public static void TryCleanupRunWork(string experimentId, string runId) =>
        DeleteDirectory(Path.Combine(RepoRoot, "benchmarks", "work", runId, experimentId), throwOnFailure: false);

    public static void TryDeleteDirectory(string path) => DeleteDirectory(path, throwOnFailure: false);

    public static string ResultPath(string experimentId) =>
        Path.Combine(ResultsDir(), experimentId + ".html");

    public static string LatestManifestPath(string experimentId) =>
        Path.Combine(ResultsDir(), experimentId + ".manifest.json");

    public static string LatestRawJsonPath(string experimentId) =>
        Path.Combine(ResultsDir(), experimentId + ".raw.json");

    public static string LatestRawCsvPath(string experimentId) =>
        Path.Combine(ResultsDir(), experimentId + ".raw.csv");

    public static string WorkerResultPath(string experimentId, string runId, BenchmarkEngine engine) =>
        Path.Combine(RawRunDir(experimentId, runId), EngineToken(engine) + ".worker.json");

    public static string ImmutableManifestPath(string experimentId, string runId) =>
        Path.Combine(RawRunDir(experimentId, runId), "manifest.json");

    public static string ImmutableRawJsonPath(string experimentId, string runId) =>
        Path.Combine(RawRunDir(experimentId, runId), "combined.raw.json");

    public static string ImmutableRawCsvPath(string experimentId, string runId) =>
        Path.Combine(RawRunDir(experimentId, runId), "samples.csv");

    public static string EngineToken(BenchmarkEngine engine) =>
        engine == BenchmarkEngine.Sqlite ? "sqlite" : "polar-db";

    public static long DirBytes(string dir)
    {
        if (!Directory.Exists(dir)) return 0L;
        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }

    private static string ResultsDir() =>
        Path.Combine(RepoRoot, "benchmarks", "results");

    private static string RawRunDir(string experimentId, string runId) =>
        Path.Combine(ResultsDir(), "raw", experimentId, runId);

    private static string EngineWorkDir(string experimentId, string runId, BenchmarkEngine engine) =>
        Path.Combine(RepoRoot, "benchmarks", "work", runId, experimentId, EngineToken(engine));

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
