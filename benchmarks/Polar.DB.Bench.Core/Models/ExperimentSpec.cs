using Polar.DB.Bench.Core.Abstractions;

namespace Polar.DB.Bench.Core.Models;

public sealed record ExperimentSpec
{
    public required string ExperimentKey { get; init; }
    public string? ResearchQuestionId { get; init; }
    public string? HypothesisId { get; init; }
    public string? Description { get; init; }
    public required DatasetSpec Dataset { get; init; }
    public required WorkloadSpec Workload { get; init; }
    public FaultProfileSpec? FaultProfile { get; init; }
    public FairnessProfileSpec? FairnessProfile { get; init; }
    public IReadOnlyList<EngineCapability>? RequiredCapabilities { get; init; }
}
