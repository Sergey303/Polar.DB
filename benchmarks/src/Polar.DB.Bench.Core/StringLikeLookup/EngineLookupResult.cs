using System.Collections.Generic;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record EngineLookupResult(
    long MatchedCount,
    long? RowsVisited,
    IReadOnlyDictionary<string, string> Diagnostics)
{
    public static EngineLookupResult FromCount(long count) =>
        new(count, null, new Dictionary<string, string>());
}
