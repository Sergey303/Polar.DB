using System.IO;
using Polar.DB.Bench.Exec.ExternalNuget;

namespace Polar.DB.Bench.Exec;

/// <summary>
/// Copy the body of RunExternalPolarDbNugetTarget into the existing place where
/// Polar.DB.Bench.Exec currently prints:
/// "through external Polar.DB NuGet runner (...)".
///
/// This file is intentionally an integration example instead of a required runtime type,
/// because target/experiment model names may differ in your repository.
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
            ExperimentPath = experimentPath,
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
}
