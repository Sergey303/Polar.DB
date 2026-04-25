using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Polar.DB.Bench.Exec.PolarDbNuget.Cli;
using Polar.DB.Bench.Exec.PolarDbNuget.Contracts;
using Polar.DB.Bench.Exec.PolarDbNuget.Isolation;
using Polar.DB.Bench.Exec.PolarDbNuget.Reflection;
using Polar.DB.Bench.Exec.PolarDbNuget.Workloads;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Execution;

internal sealed class PolarDbNugetRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public RawRunResult Execute(RunnerOptions options, DateTimeOffset startedAtUtc)
    {
        var modeText = options.Mode.ToString().ToLowerInvariant();
        var runId = RunIdFactory.Create(options.EngineKey, modeText, startedAtUtc);
        var polarDll = PackageLocator.ResolvePolarDll(
            explicitDllPath: options.PolarDllPath,
            packageId: options.PackageId,
            packageVersion: options.PackageVersion,
            tfm: options.TargetFrameworkMoniker,
            nugetCachePath: options.NugetCachePath);

        var loadContext = new NugetAssemblyLoadContext(polarDll);
        var assembly = loadContext.LoadFromAssemblyPath(polarDll);
        var probe = PolarDbAssemblyProbe.Create(assembly);

        var result = new RawRunResult
        {
            RunId = runId,
            EngineKey = options.EngineKey,
            Mode = modeText,
            Success = true,
            StartedAtUtc = startedAtUtc,
            PolarDllPath = polarDll,
            PolarAssemblyFullName = assembly.FullName,
            Environment = EnvironmentInfo.Collect(),
            Probe = probe
        };

        try
        {
            if (options.Mode == RunnerMode.Run)
            {
                var experiment = ReadExperiment(options.ExperimentPath!);
                result.ExperimentId = experiment.ExperimentId;
                result.ScenarioKey = experiment.ScenarioKey;

                PrepareWorkDirectory(options.WorkDirectory, options.KeepWorkDirectory);

                var stopwatch = Stopwatch.StartNew();
                var workloadResult = new PolarDbWorkloadHost(assembly).Execute(experiment, options.WorkDirectory);
                stopwatch.Stop();

                foreach (var item in workloadResult.Metrics)
                {
                    result.Metrics[item.Key] = item.Value;
                }

                result.Metrics["runnerElapsedMs"] = stopwatch.Elapsed.TotalMilliseconds;
                result.Artifacts.AddRange(CollectArtifacts(options.WorkDirectory));
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ErrorInfo.FromException(ex);
        }
        finally
        {
            result.EndedAtUtc = DateTimeOffset.UtcNow;
        }

        return result;
    }

    private static ExperimentDocument ReadExperiment(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException("Experiment file was not found.", full);
        }

        var experiment = JsonSerializer.Deserialize<ExperimentDocument>(File.ReadAllText(full), JsonOptions);
        return experiment ?? throw new InvalidDataException("Experiment JSON is empty or invalid.");
    }

    private static void PrepareWorkDirectory(string workDirectory, bool keepWorkDirectory)
    {
        var full = Path.GetFullPath(workDirectory);
        if (Directory.Exists(full) && !keepWorkDirectory)
        {
            Directory.Delete(full, recursive: true);
        }

        Directory.CreateDirectory(full);
    }

    private static IEnumerable<ArtifactInfo> CollectArtifacts(string workDirectory)
    {
        if (!Directory.Exists(workDirectory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(workDirectory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            yield return new ArtifactInfo
            {
                Role = GuessRole(info.Name),
                Path = Path.GetFullPath(file),
                Bytes = info.Length
            };
        }
    }

    private static string GuessRole(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("state")) return "state";
        if (lower.Contains("index")) return "secondary-index";
        if (lower.EndsWith(".db", StringComparison.Ordinal) || lower.Contains("polar")) return "primary-data";
        return "artifact";
    }
}
