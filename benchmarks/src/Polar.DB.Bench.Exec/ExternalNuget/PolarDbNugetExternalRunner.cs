using System;
using System.Collections.Generic;
using System.IO;

namespace Polar.DB.Bench.Exec.ExternalNuget;

internal sealed class PolarDbNugetExternalRunner
{
    public ExternalProcessResult Run(PolarDbNugetExternalRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runnerProjectPath = Path.GetFullPath(request.RunnerProjectPath);
        if (!File.Exists(runnerProjectPath))
        {
            throw new FileNotFoundException("External Polar.DB NuGet runner project was not found.", runnerProjectPath);
        }

        var packageVersion = request.PackageVersion;
        if (string.IsNullOrWhiteSpace(packageVersion))
        {
            packageVersion = PolarDbNugetVersionInference.TryInferPackageVersion(request.EngineKey);
        }

        if (string.IsNullOrWhiteSpace(packageVersion) && string.IsNullOrWhiteSpace(request.PolarDllPath))
        {
            throw new InvalidOperationException(
                "External Polar.DB NuGet runner requires PackageVersion or PolarDllPath. " +
                $"EngineKey='{request.EngineKey}' did not contain a NuGet version.");
        }

        var arguments = new List<string>
        {
            "run",
            "--project",
            runnerProjectPath,
            "--"
        };

        Add(arguments, "--mode", request.Mode);
        Add(arguments, "--engine-key", request.EngineKey);

        if (!string.IsNullOrWhiteSpace(request.PolarDllPath))
        {
            Add(arguments, "--polar-dll", Path.GetFullPath(request.PolarDllPath));
        }
        else
        {
            // This is the important fix: the parent Exec must pass package version explicitly.
            Add(arguments, "--package-version", packageVersion!);
            Add(arguments, "--package-id", request.PackageId);
            Add(arguments, "--tfm", request.TargetFrameworkMoniker);

            if (!string.IsNullOrWhiteSpace(request.NugetCachePath))
            {
                Add(arguments, "--nuget-cache", Path.GetFullPath(request.NugetCachePath));
            }
        }

        if (!string.Equals(request.Mode, "probe", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.ExperimentPath))
            {
                throw new InvalidOperationException("Run mode requires ExperimentPath.");
            }

            Add(arguments, "--experiment", Path.GetFullPath(request.ExperimentPath));
        }

        if (!string.IsNullOrWhiteSpace(request.WorkDirectory))
        {
            Add(arguments, "--work-dir", Path.GetFullPath(request.WorkDirectory));
        }

        Add(arguments, "--output", Path.GetFullPath(request.OutputPath));

        if (request.KeepWorkDirectory)
        {
            arguments.Add("--keep-work-dir");
        }

        return ExternalProcessRunner.Run("dotnet", arguments);
    }

    private static void Add(List<string> arguments, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Argument {name} cannot be empty.", nameof(value));
        }

        arguments.Add(name);
        arguments.Add(value);
    }
}
