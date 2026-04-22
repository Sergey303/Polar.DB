namespace Polar.DB.Bench.Core.Models;

public sealed record MetricGuardPolicy
{
    public required string MetricKey { get; init; }
    public required string Mode { get; init; }
    public double? AbsoluteMax { get; init; }
    public double? MaxRegressionPercent { get; init; }
    public double? FixedSlack { get; init; }
    public required string Severity { get; init; }
}
