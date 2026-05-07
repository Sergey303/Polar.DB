using System;
using System.Collections.Generic;
using System.Linq;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record StringLikeRunResult(
    string EngineKey,
    DateTimeOffset StartedAtUtc,
    EnvironmentSnapshot Environment,
    TimeSpan LoadElapsed,
    TimeSpan BuildElapsed,
    TimeSpan ReopenElapsed,
    IReadOnlyList<StringLikeCaseResult> Cases,
    IReadOnlyList<ArtifactInfo> Artifacts)
{
    public long TotalArtifactBytes => Artifacts.Sum(static x => x.Bytes);
}
