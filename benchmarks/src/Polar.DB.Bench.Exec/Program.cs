using System;
using System.IO;
using System.Text;
using Polar.DB.Bench.Exec;
using Polar.DB.Bench.Exec.Runtime;

if (!CliOptions.TryParse(args, out var options, out var parseError))
{
    Console.Error.WriteLine(parseError);
    Console.Error.WriteLine();
    Console.Error.WriteLine(CliOptions.UsageText);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(CliOptions.UsageText);
    return 0;
}

string benchmarksRoot;
try
{
    benchmarksRoot = ExperimentFolderResolver.FindBenchmarksRootFromCurrentDirectory();
}
catch (DirectoryNotFoundException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

if (options.SmokeCleanup)
{
    return RunSmokeCleanup(benchmarksRoot);
}

var experimentInput = options.ExperimentInput;
if (string.IsNullOrWhiteSpace(experimentInput))
{
    if (!ExperimentSelection.IsInteractiveSession())
    {
        Console.Error.WriteLine("Missing required --exp argument.");
        Console.Error.WriteLine();
        Console.Error.WriteLine(CliOptions.UsageText);
        return 2;
    }

    if (!ExperimentSelection.TrySelectExperiment(benchmarksRoot, out experimentInput, out var selectionError))
    {
        Console.Error.WriteLine(selectionError);
        return 2;
    }
}

if (!ExperimentFolderResolver.TryResolve(benchmarksRoot, experimentInput!, out var experiment, out var resolveError))
{
    Console.Error.WriteLine(resolveError);
    return 2;
}

var runId = CreateRunId(experiment.FolderName);
var paths = new RunPaths(benchmarksRoot, runId);
var runner = new ExperimentRunner();

var result = runner.Run(
    paths,
    experimentId: experiment.FolderName,
    engineKey: "benchmark",
    executeExperiment: _ =>
    {
        var exitCode = ExecApplication.RunAsync(["--exp", experiment.FolderPath]).GetAwaiter().GetResult();
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Experiment '{experiment.FolderName}' failed with exit code {exitCode}.");
        }
    });

Console.WriteLine($"Experiment: {experiment.FolderName}");
Console.WriteLine($"Spec: {experiment.SpecPath}");
Console.WriteLine($"Run id: {result.RunId}");
Console.WriteLine($"Success: {result.Success}");
Console.WriteLine($"Raw result: {paths.RawResultPath}");
Console.WriteLine($"Deleted temporary files: {result.Cleanup?.DeletedFiles ?? 0}");
Console.WriteLine($"Cleanup failures: {result.Cleanup?.FailedFiles ?? 0}");

return result.Success ? 0 : 1;

static int RunSmokeCleanup(string benchmarksRoot)
{
    var runId = CreateRunId("local-cleanup-smoke");
    var paths = new RunPaths(benchmarksRoot, runId);
    var runner = new ExperimentRunner();

    var result = runner.Run(
        paths,
        experimentId: "local-cleanup-smoke",
        engineKey: "smoke",
        executeExperiment: static runPaths =>
        {
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
}

static string CreateRunId(string experimentFolderName)
{
    var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
    var sanitized = SanitizeSegment(experimentFolderName);
    return $"{timestamp}.{sanitized}";
}

static string SanitizeSegment(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "experiment";
    }

    var builder = new StringBuilder(value.Length);
    var lastWasDash = false;

    foreach (var ch in value.Trim())
    {
        if (char.IsLetterOrDigit(ch))
        {
            builder.Append(char.ToLowerInvariant(ch));
            lastWasDash = false;
            continue;
        }

        if (!lastWasDash)
        {
            builder.Append('-');
            lastWasDash = true;
        }
    }

    var sanitized = builder.ToString().Trim('-');
    return string.IsNullOrWhiteSpace(sanitized) ? "experiment" : sanitized;
}
