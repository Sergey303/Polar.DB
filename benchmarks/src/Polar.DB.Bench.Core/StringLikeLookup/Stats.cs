using System;
using System.Collections.Generic;
using System.Linq;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public static class Stats
{
    public static double TrimmedMean(IReadOnlyList<double> values, double trimRatio = 0.1)
    {
        if (values.Count == 0) throw new ArgumentException("No values.");
        var sorted = values.OrderBy(static x => x).ToArray();
        var trim = (int)Math.Floor(sorted.Length * trimRatio);
        var kept = sorted.Skip(trim).Take(sorted.Length - trim * 2).ToArray();
        return kept.Length == 0 ? sorted.Average() : kept.Average();
    }

    public static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0) throw new ArgumentException("No values.");
        if (percentile < 0 || percentile > 100) throw new ArgumentOutOfRangeException(nameof(percentile));

        var sorted = values.OrderBy(static x => x).ToArray();
        var rank = (percentile / 100d) * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);

        if (lower == upper) return sorted[lower];

        var weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}
