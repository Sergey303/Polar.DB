using System.Collections.Generic;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public static class StringLikeQueryPlanner
{
    public static IReadOnlyList<StringLikeQueryCase> CreateDefaultCases(int pivotId)
    {
        var group = pivotId % 100;
        var sub = (pivotId / 100) % 100;
        var full = $"grp{group:D4}/sub{sub:D4}/item{pivotId:D8}";
        var groupSub = $"grp{group:D4}/sub{sub:D4}/";
        var groupOnly = $"grp{group:D4}/";
        var subOnly = $"sub{sub:D4}";

        return new[]
        {
            new StringLikeQueryCase("exact1", StringLikeQueryKind.Exact, full, full),
            new StringLikeQueryCase("prefix1", StringLikeQueryKind.Prefix, full + "%", full),
            new StringLikeQueryCase("prefixSmall", StringLikeQueryKind.Prefix, groupSub + "%", groupSub),
            new StringLikeQueryCase("prefixMedium", StringLikeQueryKind.Prefix, groupOnly + "%", groupOnly),
            new StringLikeQueryCase("containsScan", StringLikeQueryKind.Contains, "%" + subOnly + "%", subOnly)
        };
    }
}
