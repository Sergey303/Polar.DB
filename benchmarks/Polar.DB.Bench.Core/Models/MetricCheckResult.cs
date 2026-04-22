namespace Polar.DB.Bench.Core.Models;

public sealed record MetricCheckResult
{
    public required string MetricKey { get; init; }
    public required string Status { get; init; }
    public double? Actual { get; init; }
    public double? ExpectedMax { get; init; }
    public double? BaselineValue { get; init; }
    public string? Message { get; init; }
}
