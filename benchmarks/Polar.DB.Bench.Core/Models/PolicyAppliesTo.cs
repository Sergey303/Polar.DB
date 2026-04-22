namespace Polar.DB.Bench.Core.Models;

public sealed record PolicyAppliesTo
{
    public string? ExperimentKey { get; init; }
    public string? DatasetProfileKey { get; init; }
    public string? EngineKey { get; init; }
    public string? FairnessProfileKey { get; init; }
    public string? EnvironmentClass { get; init; }
}
