using System.IO;

namespace Polar.DB.Bench.Exec.ExternalNuget;

internal sealed class PolarDbNugetExternalRunRequest
{
    public string Mode { get; init; } = "run";
    public string EngineKey { get; init; } = "polar-db-nuget";
    public string? PackageVersion { get; init; }
    public string PackageId { get; init; } = "Polar.DB";
    public string TargetFrameworkMoniker { get; init; } = "netstandard2.0";
    public string? NugetCachePath { get; init; }
    public string? PolarDllPath { get; init; }
    public string? ExperimentPath { get; init; }
    public string? WorkDirectory { get; init; }
    public string OutputPath { get; init; } = Path.Combine("benchmarks", "results", "raw", "polar-db-nuget.raw.json");
    public bool KeepWorkDirectory { get; init; }

    /// <summary>
    /// Path to Polar.DB.Bench.Exec.PolarDbNuget.csproj.
    /// </summary>
    public string RunnerProjectPath { get; init; } = Path.Combine(
        "benchmarks",
        "src",
        "Polar.DB.Bench.Exec.PolarDbNuget",
        "Polar.DB.Bench.Exec.PolarDbNuget.csproj");
}
