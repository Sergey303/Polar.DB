using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Polar.DB.Bench.Exec;

public static class ExperimentFolderResolver
{
    public static string FindBenchmarksRootFromCurrentDirectory()
    {
        var current = Directory.GetCurrentDirectory();

        while (current is not null)
        {
            if (Path.GetFileName(current).Equals("benchmarks", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            var candidate = Path.Combine(current, "benchmarks");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Cannot find benchmarks directory from current working directory.");
    }

    public static bool TryResolve(
        string benchmarksRoot,
        string experimentInput,
        out ResolvedExperiment experiment,
        out string error)
    {
        experiment = default;

        if (string.IsNullOrWhiteSpace(experimentInput))
        {
            error = "Missing experiment folder value.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(benchmarksRoot))
        {
            error = "Benchmarks root is empty.";
            return false;
        }

        var candidatePath = ResolveExperimentPath(benchmarksRoot, experimentInput);
        if (File.Exists(candidatePath))
        {
            error = $"--exp must point to an experiment folder, but file was provided: '{candidatePath}'.";
            return false;
        }

        if (!Directory.Exists(candidatePath))
        {
            error = $"Experiment folder not found: '{candidatePath}'.";
            return false;
        }

        var specFiles = FindSpecFiles(candidatePath);
        if (specFiles.Count == 0)
        {
            error =
                $"Experiment spec file not found in '{candidatePath}'. " +
                "Expected one file matching 'experiment.json' or 'experiment.*.json'.";
            return false;
        }

        if (specFiles.Count > 1)
        {
            var files = string.Join(", ", specFiles.Select(Path.GetFileName));
            error =
                $"Multiple experiment spec files found in '{candidatePath}': {files}. " +
                "Cannot choose one automatically.";
            return false;
        }

        var fullFolderPath = Path.GetFullPath(candidatePath);
        var folderName = Path.GetFileName(
            fullFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        experiment = new ResolvedExperiment(
            fullFolderPath,
            folderName,
            specFiles[0]);

        error = string.Empty;
        return true;
    }

    private static string ResolveExperimentPath(string benchmarksRoot, string experimentInput)
    {
        if (Path.IsPathRooted(experimentInput))
        {
            return Path.GetFullPath(experimentInput);
        }

        if (LooksLikePath(experimentInput))
        {
            return Path.GetFullPath(experimentInput);
        }

        return Path.GetFullPath(Path.Combine(benchmarksRoot, "experiments", experimentInput));
    }

    private static bool LooksLikePath(string value)
    {
        return value.StartsWith(".", StringComparison.Ordinal)
               || value.Contains(Path.DirectorySeparatorChar)
               || value.Contains(Path.AltDirectorySeparatorChar);
    }

    private static List<string> FindSpecFiles(string experimentFolder)
    {
        var files = Directory
            .EnumerateFiles(experimentFolder, "experiment.json", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(experimentFolder, "experiment.*.json", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return files;
    }
}

public readonly record struct ResolvedExperiment(
    string FolderPath,
    string FolderName,
    string SpecPath);
