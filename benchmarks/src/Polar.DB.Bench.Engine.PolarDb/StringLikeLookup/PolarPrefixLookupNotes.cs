using System.Collections.Generic;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.PolarDb.StringLikeLookup;

public static class PolarPrefixLookupNotes
{
    public static IReadOnlyDictionary<string, string> CreateDiagnostics(
        string prefix,
        long matchedCount,
        long visitedCount,
        string? logicalEnd = null,
        string? appendOffset = null)
    {
        var bounds = PrefixBoundsBuilder.ForPrefix(prefix);
        var result = new Dictionary<string, string>
        {
            ["comparator"] = "ordinal-prefix-range",
            ["lowerInclusive"] = bounds.LowerInclusive,
            ["upperExclusive"] = bounds.UpperExclusive ?? "<none>",
            ["boundary"] = "first-key-greater-or-equal-prefix",
            ["matchedCount"] = matchedCount.ToString(),
            ["visitedCount"] = visitedCount.ToString()
        };

        if (logicalEnd is not null) result["logicalEnd"] = logicalEnd;
        if (appendOffset is not null) result["appendOffset"] = appendOffset;
        return result;
    }
}
