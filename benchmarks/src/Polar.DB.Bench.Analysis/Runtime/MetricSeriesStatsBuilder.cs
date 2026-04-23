using System.Collections.Generic;
using System.Linq;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Builds aggregate stats for a metric across measured runs.
/// Raw runs are not modified. This produces derived summary values only.
/// </summary>
internal static class MetricSeriesStatsBuilder
{
    public static MetricSeriesStats Build(IReadOnlyList<double?> samples)
    {
        var values = samples
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();

        if (values.Length == 0)
        {
            return new MetricSeriesStats
            {
                Count = samples.Count,
                MissingCount = samples.Count,
                Min = null,
                Max = null,
                Average = null,
                Median = null
            };
        }

        return new MetricSeriesStats
        {
            Count = samples.Count,
            MissingCount = samples.Count - values.Length,
            Min = values[0],
            Max = values[^1],
            Average = values.Average(),
            Median = ReadMedian(values)
        };
    }

    private static double ReadMedian(double[] sortedValues)
    {
        if (sortedValues.Length == 0)
        {
            return 0.0;
        }

        var middle = sortedValues.Length / 2;
        if (sortedValues.Length % 2 == 1)
        {
            return sortedValues[middle];
        }

        return (sortedValues[middle - 1] + sortedValues[middle]) / 2.0;
    }
}
