using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using Polar.DB.Bench.Engine.PolarDb;
using Polar.DB.Bench.Engine.Sqlite;

namespace Polar.DB.Bench.Exec.Runtime;

public static class ExecApplication
{
    private const string WarmupRunRole = "warmup";
    private const string MeasuredRunRole = "measured";

    private const string SimpleUsageText =
        "Usage simple mode: --exp <experiment-dir|experiment.json> " +
        "[--env <class>] [--comparison-set <id>] [--warmup-count <n>] [--measured-count <n>]";

    public static async Task<int> RunAsync(string[] args)
    {
        var argMap = BuildArgumentMap(args);
        if (argMap.TryGetValue("--exp", out var experimentPath) && !string.IsNullOrWhiteSpace(experimentPath))
        {
            try
            {
                return await RunSimpleExperimentAsync(argMap, experimentPath);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(SimpleUsageText);
                return 2;
            }
            catch (NotSupportedException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        var options = ExecOptions.Parse(args);
        if (!options.IsValid(out var validationError))
        {
            Console.Error.WriteLine(validationError);
            Console.Error.WriteLine(ExecOptions.UsageText);
            return 2;
        }

        return await RunSingleTargetSafeAsync(options);
    }

    private static async Task<int> RunSimpleExperimentAsync(
        IReadOnlyDictionary<string, string> argMap,
        string experimentPath)
    {
        var specPath = ExperimentSpecLoader.ResolveSpecPath(experimentPath);
        var experimentDirectory =
            ExperimentSpecLoader.TryResolveExperimentDirectory(experimentPath)
            ?? throw new InvalidOperationException(
                "Simple mode requires canonical experiment path: experiment folder or experiment.json.");

        var manifest = await LoadManifestAsync(specPath);
        if (manifest.Targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Experiment '{manifest.ExperimentKey}' does not declare any targets.");
        }

        var comparisonSetId =
            GetOptional(argMap, "--comparison-set")
            ?? $"simple-{DateTimeOffset.UtcNow:yyyyMMddTHHmmss}";
        var environmentClass = GetOptional(argMap, "--env") ?? "local";
        var warmupCount = ParseOptionalNonNegativeInt(argMap, "--warmup-count");
        var measuredCount = ParseOptionalPositiveInt(argMap, "--measured-count");

        var repositoryRoot =
            ResolveRepositoryRoot(experimentDirectory)
            ?? ResolveRepositoryRoot(Environment.CurrentDirectory)
            ?? throw new InvalidOperationException("Failed to resolve repository root (.git).");

        var benchmarksRoot = ResolveBenchmarksRoot(experimentDirectory, repositoryRoot);
        var analysisProjectPath = Path.Combine(
            benchmarksRoot,
            "src",
            "Polar.DB.Bench.Analysis",
            "Polar.DB.Bench.Analysis.csproj");
        var chartsProjectPath = Path.Combine(
            benchmarksRoot,
            "src",
            "Polar.DB.Bench.Charts",
            "Polar.DB.Bench.Charts.csproj");

        if (!File.Exists(analysisProjectPath))
        {
            throw new InvalidOperationException(
                $"Analysis project not found: '{analysisProjectPath}'.");
        }

        if (!File.Exists(chartsProjectPath))
        {
            throw new InvalidOperationException(
                $"Charts project not found: '{chartsProjectPath}'.");
        }

        var experimentSlug = Path.GetFileName(
            experimentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (var targetKey in manifest.Targets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var workingDirectory = Path.Combine(
                benchmarksRoot,
                "work",
                experimentSlug,
                targetKey);

            var options = new ExecOptions
            {
                EngineKey = targetKey,
                SpecPath = experimentDirectory,
                WorkingDirectory = workingDirectory,
                RawResultsDirectory = null,
                EnvironmentClass = environmentClass,
                ComparisonSetId = comparisonSetId,
                WarmupCount = warmupCount,
                MeasuredCount = measuredCount
            };

            Console.WriteLine(
                $"==> Running target '{targetKey}' for experiment '{manifest.ExperimentKey}'");

            var exitCode = await RunSingleTargetSafeAsync(options);
            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        Console.WriteLine("==> Running analysis");
        var analysisExitCode = await RunDotNetProjectAsync(
            repositoryRoot,
            analysisProjectPath,
            new[]
            {
                "--raw-dir", experimentDirectory,
                "--compare-experiment", manifest.ExperimentKey,
                "--compare-set", comparisonSetId
            });

        if (analysisExitCode != 0)
        {
            return analysisExitCode;
        }

        Console.WriteLine("==> Running charts");
        var chartsExitCode = await RunDotNetProjectAsync(
            repositoryRoot,
            chartsProjectPath,
            new[]
            {
                "--comparisons", Path.Combine(experimentDirectory, "comparisons"),
                "--reports-out", experimentDirectory
            });

        return chartsExitCode;
    }

    private static async Task<int> RunSingleTargetSafeAsync(ExecOptions options)
    {
        try
        {
            return await RunSingleTargetCoreAsync(options);
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine(
                $"Target '{options.EngineKey ?? "<auto>"}' is not supported for spec '{options.SpecPath}'.");
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static async Task<int> RunSingleTargetCoreAsync(ExecOptions options)
    {
        var spec = await ExperimentSpecLoader.LoadAsync(options.SpecPath!, options.EngineKey);
        var (engineFamily, runtime) = EngineRuntimeResolver.Resolve(spec);
        var targetKey = spec.TargetKey;
        var rawResultsDirectory = ExperimentSpecLoader.ResolveRawResultsDirectory(
            options.SpecPath!,
            options.RawResultsDirectory);

        Directory.CreateDirectory(rawResultsDirectory);
        Directory.CreateDirectory(options.WorkingDirectory!);

        var repositoryRoot =
            ResolveRepositoryRoot(options.WorkingDirectory!)
            ?? ResolveRepositoryRoot(Environment.CurrentDirectory)
            ?? Path.GetFullPath(Path.Combine(options.WorkingDirectory!, ".."));

        var workspace = new RunWorkspace
        {
            RootDirectory = repositoryRoot,
            WorkingDirectory = options.WorkingDirectory!,
            RawResultsDirectory = rawResultsDirectory,
            EnvironmentClass = options.EnvironmentClass,
            ArtifactsDirectory = Path.Combine(options.WorkingDirectory!, "artifacts")
        };

        Directory.CreateDirectory(workspace.ArtifactsDirectory);

        var adapter = CreateAdapter(engineFamily);
        var executionPlan = BuildExecutionPlan(options);
        var measuredResults = new List<RunResult>(executionPlan.MeasuredCount);

        for (var i = 0; i < executionPlan.TotalCount; i++)
        {
            var runRole = i < executionPlan.WarmupCount ? WarmupRunRole : MeasuredRunRole;
            var sequenceNumber = i + 1;

            await using var run = adapter.CreateRun(spec, workspace);
            var rawResult = await run.ExecuteAsync();
            var taggedResult = AttachSeriesInfo(
                rawResult,
                executionPlan.ComparisonSetId,
                sequenceNumber,
                runRole,
                executionPlan.WarmupCount,
                executionPlan.MeasuredCount);

            taggedResult = AttachEngineRuntimeInfo(taggedResult, targetKey, engineFamily, runtime);

            var rawPath = BuildRawPath(
                workspace,
                taggedResult,
                runRole,
                sequenceNumber,
                executionPlan.TotalCount > 1);

            await using var stream = File.Create(rawPath);
            await JsonSerializer.SerializeAsync(stream, taggedResult, JsonDefaults.Default);

            Console.WriteLine($"Raw result written: {rawPath}");
            if (string.Equals(runRole, MeasuredRunRole, StringComparison.OrdinalIgnoreCase))
            {
                measuredResults.Add(taggedResult);
            }
        }

        return measuredResults.All(x => x.TechnicalSuccess) ? 0 : 1;
    }

    private static IStorageEngineAdapter CreateAdapter(string engineKey)
    {
        return engineKey switch
        {
            "synthetic" => new SyntheticStorageEngineAdapter(),
            "polar-db" => new PolarDbStorageEngineAdapter(),
            "sqlite" => new SqliteStorageEngineAdapter(),
            _ => throw new NotSupportedException(
                $"Engine '{engineKey}' is not available. Supported: synthetic, polar-db, sqlite.")
        };
    }

    private static string? ResolveRepositoryRoot(string startDirectory)
    {
        var fullPath = Path.GetFullPath(startDirectory);
        var directory = new DirectoryInfo(fullPath);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ResolveBenchmarksRoot(string experimentDirectory, string repositoryRoot)
    {
        var fullExperimentDirectory = Path.GetFullPath(experimentDirectory);
        var experimentsDirectory = Directory.GetParent(fullExperimentDirectory)?.FullName;
        if (!string.IsNullOrWhiteSpace(experimentsDirectory) &&
            Path.GetFileName(experimentsDirectory)
                .Equals("experiments", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarksRoot = Directory.GetParent(experimentsDirectory)?.FullName;
            if (!string.IsNullOrWhiteSpace(benchmarksRoot))
            {
                return benchmarksRoot;
            }
        }

        var repositoryBenchmarksRoot = Path.Combine(repositoryRoot, "benchmarks");
        if (Directory.Exists(repositoryBenchmarksRoot))
        {
            return repositoryBenchmarksRoot;
        }

        throw new InvalidOperationException(
            "Failed to resolve benchmarks root from experiment path.");
    }

    private static async Task<ExperimentManifest> LoadManifestAsync(string specPath)
    {
        await using var stream = File.OpenRead(specPath);
        var manifest = await JsonSerializer.DeserializeAsync<ExperimentManifest>(stream, JsonDefaults.Default);
        return manifest ?? throw new InvalidOperationException(
            $"Failed to deserialize experiment manifest from '{specPath}'.");
    }

    private static async Task<int> RunDotNetProjectAsync(
        string workingDirectory,
        string projectPath,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--");

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException(
                                $"Failed to start dotnet process for '{projectPath}'.");

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static SeriesExecutionPlan BuildExecutionPlan(ExecOptions options)
    {
        var hasComparisonSet = !string.IsNullOrWhiteSpace(options.ComparisonSetId);
        var warmupCount = options.WarmupCount ?? (hasComparisonSet ? 1 : 0);
        var measuredCount = options.MeasuredCount ?? (hasComparisonSet ? 3 : 1);

        if (warmupCount < 0)
        {
            throw new InvalidOperationException("--warmup-count must be >= 0.");
        }

        if (measuredCount <= 0)
        {
            throw new InvalidOperationException("--measured-count must be >= 1.");
        }

        var comparisonSetId = options.ComparisonSetId;
        if (string.IsNullOrWhiteSpace(comparisonSetId) && warmupCount + measuredCount > 1)
        {
            comparisonSetId = $"auto-{DateTimeOffset.UtcNow:yyyyMMddTHHmmss}";
        }

        return new SeriesExecutionPlan(
            comparisonSetId,
            warmupCount,
            measuredCount,
            warmupCount + measuredCount);
    }

    private static RunResult AttachSeriesInfo(
        RunResult result,
        string? comparisonSetId,
        int sequenceNumber,
        string runRole,
        int warmupCount,
        int measuredCount)
    {
        var tags = result.Tags is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(result.Tags, StringComparer.OrdinalIgnoreCase);

        tags["warmupCount"] = warmupCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        tags["measuredCount"] = measuredCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        tags["role"] = runRole;

        return result with
        {
            ComparisonSetId = comparisonSetId,
            RunSeriesSequenceNumber = sequenceNumber,
            RunRole = runRole,
            Tags = tags
        };
    }

    private static RunResult AttachEngineRuntimeInfo(
        RunResult result,
        string targetKey,
        string engineFamily,
        EngineRuntimeDescriptor runtime)
    {
        var diagnostics = result.EngineDiagnostics is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(result.EngineDiagnostics, StringComparer.OrdinalIgnoreCase);

        diagnostics["runtimeSource"] = runtime.Source;
        if (!string.IsNullOrWhiteSpace(runtime.Nuget))
        {
            diagnostics["runtimeNuget"] = runtime.Nuget;
        }

        return result with
        {
            EngineKey = targetKey,
            Runtime = runtime,
            EngineDiagnostics = diagnostics
        };
    }

    private static string BuildRawPath(
        RunWorkspace workspace,
        RunResult runResult,
        string runRole,
        int sequenceNumber,
        bool includeSeriesSuffix)
    {
        var timestampToken = runResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var fileName = includeSeriesSuffix
            ? $"{timestampToken}__{runResult.EngineKey}__{runRole}-{sequenceNumber:D2}.run.json"
            : $"{timestampToken}__{runResult.EngineKey}.run.json";
        var rawPath = Path.Combine(workspace.RawResultsDirectory, fileName);

        if (!File.Exists(rawPath))
        {
            return rawPath;
        }

        var ext = ".run.json";
        var baseName = rawPath[..^ext.Length];
        var attempt = 2;
        var candidate = $"{baseName}.v{attempt}{ext}";
        while (File.Exists(candidate))
        {
            attempt++;
            candidate = $"{baseName}.v{attempt}{ext}";
        }

        return candidate;
    }

    private static Dictionary<string, string> BuildArgumentMap(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2)
        {
            map[args[i]] = args[i + 1];
        }

        return map;
    }

    private static string? GetOptional(IReadOnlyDictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static int? ParseOptionalNonNegativeInt(
        IReadOnlyDictionary<string, string> map,
        string key)
    {
        if (!map.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!int.TryParse(text, out var value) || value < 0)
        {
            throw new InvalidOperationException($"{key} must be >= 0.");
        }

        return value;
    }

    private static int? ParseOptionalPositiveInt(
        IReadOnlyDictionary<string, string> map,
        string key)
    {
        if (!map.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!int.TryParse(text, out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{key} must be >= 1.");
        }

        return value;
    }

    private readonly record struct SeriesExecutionPlan(
        string? ComparisonSetId,
        int WarmupCount,
        int MeasuredCount,
        int TotalCount);
}