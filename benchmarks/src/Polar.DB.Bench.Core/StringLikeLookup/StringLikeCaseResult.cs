using System.Collections.Generic;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record StringLikeCaseResult(
    string QueryKey,
    StringLikeQueryKind Kind,
    string Pattern,
    long MatchedCount,
    double TrimmedMeanMs,
    double P95Ms,
    double MinMs,
    double MaxMs,
    IReadOnlyDictionary<string, string> Diagnostics);
