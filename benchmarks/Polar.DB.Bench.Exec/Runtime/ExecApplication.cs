using System.Text.Json;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using Polar.DB.Bench.Engine.PolarDb;
using Polar.DB.Bench.Engine.Sqlite;

namespace Polar.DB.Bench.Exec.Runtime;

public static class ExecApplication
{
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
        Directory.CreateDirectory(workspace.ArtifactsDirectory!);

        var adapter = CreateAdapter(options.EngineKey!);
        await using var run = adapter.CreateRun(spec, workspace);
        var rawResult = await run.ExecuteAsync();

        var timestampToken = rawResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var rawPath = ResultPathBuilder.BuildRawResultPath(
            workspace.RawResultsDirectory,
            timestampToken,
            rawResult.ExperimentKey,
            rawResult.DatasetProfileKey,
            rawResult.EngineKey,
            rawResult.Environment.EnvironmentClass);

        await using var stream = File.Create(rawPath);
        await JsonSerializer.SerializeAsync(stream, rawResult, JsonDefaults.Default);

        Console.WriteLine($"Raw result written: {rawPath}");
        return rawResult.TechnicalSuccess ? 0 : 1;
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
}
