using System.Text.Json;
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

    public static async Task<int> RunAsync(string[] args)
    {
        var options = ExecOptions.Parse(args);
        if (!options.IsValid(out var validationError))
        {
            Console.Error.WriteLine(validationError);
            Console.Error.WriteLine(ExecOptions.UsageText);
            return 2;
        }

        var spec = await LoadSpecAsync(options.SpecPath!);
        var (engineKey, runtime) = EngineRuntimeResolver.Resolve(options.EngineKey, spec);
        Directory.CreateDirectory(options.RawResultsDirectory!);
        Directory.CreateDirectory(options.WorkingDirectory!);

        var repositoryRoot =
            ResolveRepositoryRoot(options.WorkingDirectory!)
            ?? ResolveRepositoryRoot(Environment.CurrentDirectory)
            ?? Path.GetFullPath(Path.Combine(options.WorkingDirectory!, ".."));

        var workspace = new RunWorkspace
        {
            RootDirectory = repositoryRoot,
            WorkingDirectory = options.WorkingDirectory!,
            RawResultsDirectory = options.RawResultsDirectory!,
            EnvironmentClass = options.EnvironmentClass,
            ArtifactsDirectory = Path.Combine(options.WorkingDirectory!, "artifacts")
        };
        Directory.CreateDirectory(workspace.ArtifactsDirectory);

        var adapter = CreateAdapter(engineKey);
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
            taggedResult = AttachEngineRuntimeInfo(taggedResult, engineKey, runtime);

            var rawPath = BuildRawPath(workspace, taggedResult, runRole, sequenceNumber, executionPlan.TotalCount > 1);
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

    private static async Task<ExperimentSpec> LoadSpecAsync(string specPath)
    {
        await using var stream = File.OpenRead(specPath);
        var spec = await JsonSerializer.DeserializeAsync<ExperimentSpec>(stream, JsonDefaults.Default);
        return spec ?? throw new InvalidOperationException("Failed to deserialize experiment spec.");
    }

    private static IStorageEngineAdapter CreateAdapter(string engineKey)
    {
        return engineKey switch
        {
            "synthetic" => new SyntheticStorageEngineAdapter(),
            "polar-db" => new PolarDbStorageEngineAdapter(),
            "sqlite" => new SqliteStorageEngineAdapter(),
            _ => throw new NotSupportedException($"Engine '{engineKey}' is not available. Supported: synthetic, polar-db, sqlite.")
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
        string engineKey,
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
            EngineKey = engineKey,
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
        if (includeSeriesSuffix)
        {
            timestampToken = $"{timestampToken}__{sequenceNumber:D2}-{runRole}";
        }

        var rawPath = ResultPathBuilder.BuildRawResultPath(
            workspace.RawResultsDirectory,
            timestampToken,
            runResult.ExperimentKey,
            runResult.DatasetProfileKey,
            runResult.EngineKey,
            runResult.Environment.EnvironmentClass);

        if (!File.Exists(rawPath))
        {
            return rawPath;
        }

        var ext = ".run.json";
        var baseName = rawPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
            ? rawPath[..^ext.Length]
            : rawPath;
        var attempt = 2;
        var candidate = $"{baseName}.v{attempt}{ext}";
        while (File.Exists(candidate))
        {
            attempt++;
            candidate = $"{baseName}.v{attempt}{ext}";
        }

        return candidate;
    }

    private readonly record struct SeriesExecutionPlan(
        string? ComparisonSetId,
        int WarmupCount,
        int MeasuredCount,
        int TotalCount);
}
