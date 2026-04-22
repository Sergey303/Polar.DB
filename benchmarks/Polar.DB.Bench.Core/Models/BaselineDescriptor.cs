namespace Polar.DB.Bench.Core.Models;

public sealed record BaselineDescriptor
{
    public required string BaselineId { get; init; }
    public required string ExperimentKey { get; init; }
    public required string DatasetProfileKey { get; init; }
    public required string EngineKey { get; init; }
    public required string FairnessProfileKey { get; init; }
    public required string EnvironmentClass { get; init; }
    public required Dictionary<string, double> Metrics { get; init; }
}
