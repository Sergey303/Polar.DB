using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Polar.DB.Bench.Exec.ExternalNuget;

/// <summary>
/// Resolves the JSON experiment path that must be passed to the external Polar.DB NuGet runner.
///
/// The external runner is a separate process and therefore cannot reuse an in-memory experiment object
/// from Polar.DB.Bench.Exec. It must receive --experiment &lt;path&gt;.
/// </summary>
internal static class PolarDbNugetExperimentPath
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string RequireExisting(string? experimentPath)
    {
        if (string.IsNullOrWhiteSpace(experimentPath))
        {
            throw new InvalidOperationException(
                "External Polar.DB NuGet runner requires an experiment JSON path. " +
                "Pass --experiment <path> to the child process, or materialize the selected experiment to a temporary JSON file before launching it.");
        }

        var fullPath = Path.GetFullPath(experimentPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Experiment JSON file was not found.", fullPath);
        }

        return fullPath;
    }

    public static string WriteSnapshot<TExperiment>(
        TExperiment experiment,
        string experimentId,
        string workRoot,
        JsonSerializerOptions? jsonOptions = null)
    {
        if (experiment is null)
        {
            throw new ArgumentNullException(nameof(experiment));
        }

        if (string.IsNullOrWhiteSpace(experimentId))
        {
            experimentId = "experiment";
        }

        var safeExperimentId = MakeSafeFileName(experimentId);
        var directory = Path.Combine(Path.GetFullPath(workRoot), ".external-experiments");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, safeExperimentId + ".experiment.json");
        var json = JsonSerializer.Serialize(experiment, jsonOptions ?? DefaultJsonOptions);
        File.WriteAllText(path, json);
        return path;
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "experiment" : result;
    }
}
