using System.Text.Json;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Exec.Runtime;

public static class ExperimentSpecLoader
{
    private const string ManifestFileName = "experiment.json";
    private const string RawDirectoryName = "raw";

    public static async Task<ExperimentSpec> LoadAsync(
        string specPath,
        string? cliTarget,
        CancellationToken cancellationToken = default)
    {
        var resolvedSpecPath = ResolveSpecPath(specPath);

        await using var stream = File.OpenRead(resolvedSpecPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Experiment spec JSON must be an object.");
        }

        if (document.RootElement.TryGetProperty("targets", out _))
        {
            var manifest = document.RootElement.Deserialize<ExperimentManifest>(JsonDefaults.Default);
            if (manifest is null)
            {
                throw new InvalidOperationException("Failed to deserialize experiment manifest.");
            }

            return ConvertManifestToSpec(manifest, cliTarget);
        }

        var legacySpec = document.RootElement.Deserialize<ExperimentSpec>(JsonDefaults.Default);
        return legacySpec ?? throw new InvalidOperationException("Failed to deserialize experiment spec.");
    }

    public static string ResolveSpecPath(string specPath)
    {
        if (string.IsNullOrWhiteSpace(specPath))
        {
            throw new InvalidOperationException("Missing --spec path.");
        }

        if (Directory.Exists(specPath))
        {
            var manifestPath = Path.Combine(specPath, ManifestFileName);
            if (File.Exists(manifestPath))
            {
                return manifestPath;
            }

            throw new InvalidOperationException(
                $"Experiment directory '{specPath}' does not contain '{ManifestFileName}'.");
        }

        if (File.Exists(specPath))
        {
            return specPath;
        }

        throw new InvalidOperationException($"Missing or invalid --spec path: '{specPath}'.");
    }

    public static string? TryResolveExperimentDirectory(string specPath)
    {
        if (string.IsNullOrWhiteSpace(specPath))
        {
            return null;
        }

        if (Directory.Exists(specPath))
        {
            var fullDirectoryPath = Path.GetFullPath(specPath);
            var manifestPath = Path.Combine(fullDirectoryPath, ManifestFileName);
            return File.Exists(manifestPath) ? fullDirectoryPath : null;
        }

        if (File.Exists(specPath))
        {
            var fullFilePath = Path.GetFullPath(specPath);
            if (!fullFilePath.EndsWith(ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.GetDirectoryName(fullFilePath);
        }

        return null;
    }

    public static string ResolveRawResultsDirectory(string specPath, string? rawResultsDirectory)
    {
        if (!string.IsNullOrWhiteSpace(rawResultsDirectory))
        {
            return Path.GetFullPath(rawResultsDirectory);
        }

        var experimentDirectory = TryResolveExperimentDirectory(specPath);
        if (string.IsNullOrWhiteSpace(experimentDirectory))
        {
            throw new InvalidOperationException(
                "Missing --raw-out. For non-canonical spec paths, raw output directory must be provided explicitly.");
        }

        return Path.Combine(experimentDirectory, RawDirectoryName);
    }

    private static ExperimentSpec ConvertManifestToSpec(ExperimentManifest manifest, string? cliTarget)
    {
        if (manifest.Targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Experiment '{manifest.ExperimentKey}' does not declare any targets.");
        }

        var selectedTargetKey = ResolveTargetKey(cliTarget, manifest.Targets);
        var targetSpec = manifest.Targets[selectedTargetKey];

        return new ExperimentSpec
        {
            ExperimentKey = manifest.ExperimentKey,
            ResearchQuestionId = manifest.ResearchQuestionId,
            HypothesisId = manifest.HypothesisId,
            Description = manifest.Description,
            TargetKey = selectedTargetKey,
            Engine = targetSpec.Engine,
            Nuget = NormalizeNuget(targetSpec.Nuget),
            Dataset = manifest.Dataset,
            Workload = manifest.Workload,
            FaultProfile = manifest.FaultProfile,
            FairnessProfile = manifest.FairnessProfile,
            RequiredCapabilities = manifest.RequiredCapabilities
        };
    }

    private static string ResolveTargetKey(
        string? cliTarget,
        IReadOnlyDictionary<string, ExperimentTargetSpec> targets)
    {
        var normalizedCliTarget = Normalize(cliTarget);
        if (normalizedCliTarget is not null)
        {
            // First try exact target key match.
            foreach (var targetKey in targets.Keys)
            {
                if (targetKey.Equals(normalizedCliTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return targetKey;
                }
            }

            // Then try engine family match (for backward compat with --engine).
            foreach (var (targetKey, targetSpec) in targets)
            {
                if (targetSpec.Engine.Equals(normalizedCliTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return targetKey;
                }
            }

            var configured = string.Join(", ", targets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                $"Target '{normalizedCliTarget}' is not configured in experiment manifest. Configured targets: {configured}.");
        }

        if (targets.Count == 1)
        {
            return targets.Keys.First();
        }

        var targetList = string.Join(", ", targets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"Experiment manifest defines multiple targets ({targetList}). Pass --engine <target-key>.");
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }

    private static string? NormalizeNuget(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
