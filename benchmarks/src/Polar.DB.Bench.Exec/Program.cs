using System;
using System.IO;
using Polar.DB.Bench.Exec;

var benchmarksRoot = FindBenchmarksRoot(args);
var runId = CreateRunId("local-cleanup-smoke");

var paths = new RunPaths(benchmarksRoot, runId);

var runner = new ExperimentRunner();

var result = runner.Run(
    paths,
    experimentId: "local-cleanup-smoke",
    engineKey: "smoke",
    executeExperiment: static runPaths =>
    {
        // Ниже — имитация временных файлов движков.
        // В реальном эксперименте это создаст Polar.DB adapter / SQLite adapter.

        File.WriteAllText(Path.Combine(runPaths.DataDirectory, "polar.db"), "temporary polar db data");
        File.WriteAllText(Path.Combine(runPaths.DataDirectory, "sample.db"), "temporary sqlite db");
        File.WriteAllText(Path.Combine(runPaths.DataDirectory, "sample.db-wal"), "temporary sqlite wal");
        File.WriteAllText(Path.Combine(runPaths.DataDirectory, "sample.db-shm"), "temporary sqlite shm");
        File.WriteAllText(Path.Combine(runPaths.WorkDirectory, "sample.sqlite"), "temporary sqlite file");
        File.WriteAllText(Path.Combine(runPaths.WorkDirectory, "sample.sqlite-wal"), "temporary sqlite wal");
        File.WriteAllText(Path.Combine(runPaths.WorkDirectory, "sample.sqlite-shm"), "temporary sqlite shm");
        File.WriteAllText(Path.Combine(runPaths.ArtifactsDirectory, "sequence.state"), "temporary state");
        File.WriteAllText(Path.Combine(runPaths.ArtifactsDirectory, "lookup.index"), "temporary index");
        File.WriteAllText(Path.Combine(runPaths.ArtifactsDirectory, "data.pdbbin"), "temporary polar binary");
        File.WriteAllText(Path.Combine(runPaths.ArtifactsDirectory, "data.pdbstate"), "temporary polar state");
    });

Console.WriteLine($"Run id: {result.RunId}");
Console.WriteLine($"Success: {result.Success}");
Console.WriteLine($"Raw result: {paths.RawResultPath}");
Console.WriteLine($"Deleted temporary files: {result.Cleanup?.DeletedFiles ?? 0}");
Console.WriteLine($"Cleanup failures: {result.Cleanup?.FailedFiles ?? 0}");

return result.Success ? 0 : 1;

static string FindBenchmarksRoot(string[] args)
{
    if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        return Path.GetFullPath(args[0]);

    var current = Directory.GetCurrentDirectory();

    while (current is not null)
    {
        if (Path.GetFileName(current).Equals("benchmarks", StringComparison.OrdinalIgnoreCase))
            return current;

        var candidate = Path.Combine(current, "benchmarks");
        if (Directory.Exists(candidate))
            return candidate;

        current = Directory.GetParent(current)?.FullName;
    }

    throw new DirectoryNotFoundException(
        "Cannot find benchmarks directory. Pass benchmarks root path as the first argument.");
}

static string CreateRunId(string scenario)
{
    var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
    return $"{timestamp}.{scenario}";
}