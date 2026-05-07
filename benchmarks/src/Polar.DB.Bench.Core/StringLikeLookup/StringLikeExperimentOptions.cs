using System;
using System.Collections.Generic;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record StringLikeExperimentOptions(
    StringLikeDatasetOptions Dataset,
    int WarmupIterations,
    int MeasuredIterations,
    string? GitCommit,
    string? GitBranch,
    IReadOnlyList<StringLikeQueryCase> Queries)
{
    public void Validate()
    {
        Dataset.Validate();
        if (WarmupIterations < 0) throw new ArgumentOutOfRangeException(nameof(WarmupIterations));
        if (MeasuredIterations <= 0) throw new ArgumentOutOfRangeException(nameof(MeasuredIterations));
        if (Queries.Count == 0) throw new ArgumentException("At least one query is required.");

        foreach (var query in Queries)
        {
            query.Validate();
        }
    }
}
