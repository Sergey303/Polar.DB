using System.IO;
using Polar.DB.Bench.Exec.ExternalNuget;

namespace Polar.DB.Bench.Exec;

/// <summary>
/// Integration example for the existing parent Polar.DB.Bench.Exec code.
///
/// Use the body of RunExternalPolarDbNugetTarget in the place that currently prints:
/// "through external Polar.DB NuGet runner (...)".
///
/// The important fix is that the parent process must pass the complete launch contract:
///   --engine-key
///   --package-version or --polar-dll
///   --experiment
///   --work-dir
///   --output
/// </summary>
internal static class ExternalPolarDbNugetTargetPatchExample
{
    public static int RunExternalPolarDbNugetTarget(
        string engineKey,
        string? packageVersion,
        string experimentPath,
        string workDirectory,
        string outputPath,
        string? runnerProjectPath = null,
        string? nugetCachePath = null,
        bool keepWorkDirectory = false)
    {
        var request = new PolarDbNugetExternalRunRequest
        {
            Mode = "run",
            EngineKey = engineKey,
            PackageVersion = packageVersion ?? PolarDbNugetVersionInference.TryInferPackageVersion(engineKey),
            ExperimentPath = PolarDbNugetExperimentPath.RequireExisting(experimentPath),
            WorkDirectory = workDirectory,
            OutputPath = outputPath,
            RunnerProjectPath = string.IsNullOrWhiteSpace(runnerProjectPath)
                ? Path.Combine("benchmarks", "src", "Polar.DB.Bench.Exec.PolarDbNuget", "Polar.DB.Bench.Exec.PolarDbNuget.csproj")
                : runnerProjectPath,
            NugetCachePath = nugetCachePath,
            KeepWorkDirectory = keepWorkDirectory
        };

        var result = new PolarDbNugetExternalRunner().Run(request);
        return result.ExitCode;
    }

    public static int RunExternalPolarDbNugetTargetWithExperimentSnapshot<TExperiment>(
        string engineKey,
        string? packageVersion,
        TExperiment experiment,
        string experimentId,
        string workDirectory,
        string outputPath,
        string? runnerProjectPath = null,
        string? nugetCachePath = null,
        bool keepWorkDirectory = false)
    {
        var experimentPath = PolarDbNugetExperimentPath.WriteSnapshot(
            experiment,
            experimentId,
            workDirectory);

        return RunExternalPolarDbNugetTarget(
            engineKey,
            packageVersion,
            experimentPath,
            workDirectory,
            outputPath,
            runnerProjectPath,
            nugetCachePath,
            keepWorkDirectory);
    }
}
