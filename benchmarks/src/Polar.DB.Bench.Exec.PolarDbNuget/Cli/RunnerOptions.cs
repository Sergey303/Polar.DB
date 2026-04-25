namespace Polar.DB.Bench.Exec.PolarDbNuget.Cli;

internal sealed class RunnerOptions
{
    public bool ShowHelp { get; init; }
    public RunnerMode Mode { get; init; } = RunnerMode.Run;
    public string EngineKey { get; init; } = "polar-db-nuget";
    public string? PackageVersion { get; init; }
    public string PackageId { get; init; } = "Polar.DB";
    public string TargetFrameworkMoniker { get; init; } = "netstandard2.0";
    public string? NugetCachePath { get; init; }
    public string? PolarDllPath { get; init; }
    public string? ExperimentPath { get; init; }
    public string WorkDirectory { get; init; } = Path.Combine("benchmarks", ".work", "polar-db-nuget");
    public string OutputPath { get; init; } = Path.Combine("benchmarks", "results", "raw", "polar-db-nuget.raw.json");
    public bool KeepWorkDirectory { get; init; }
}
