using System;
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
    private const double OutlierZScoreThreshold = 3.5;
    private const double MadEpsilon = 1e-12;
    private const double P50Epsilon = 1e-12;
    private const double RobustZScale = 0.6745;

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

        var median = ReadMedian(values);
        var p95 = ReadPercentile(values, 0.95);
        var p99 = ReadPercentile(values, 0.99);
        var trimmedMean10 = ComputeTrimmedMean(values, 0.10);
        var mad = ComputeMad(values, median);
        var jitterRatio = ComputeJitterRatio(p95, median);
        var (outlierCount, outlierPercent) = ComputeOutliers(values, median, mad);

        return new MetricSeriesStats
        {
            Count = samples.Count,
            MissingCount = samples.Count - values.Length,
            Min = values[0],
            Max = values[^1],
            Average = values.Average(),
            Median = median,
            P50 = median,
            P95 = p95,
            P99 = p99,
            TrimmedMean10 = trimmedMean10,
            Mad = mad,
            JitterRatio = jitterRatio,
            OutlierCount = outlierCount,
            OutlierPercent = outlierPercent
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

    /// <summary>
    /// Computes the percentile value from a sorted array using linear interpolation.
    /// </summary>
    private static double ReadPercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0.0;
        }

        if (sortedValues.Length == 1)
        {
            return sortedValues[0];
        }

        var index = percentile * (sortedValues.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var fraction = index - lower;
        return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
    }

    /// <summary>
    /// Computes trimmed mean: average after removing the lowest and highest <paramref name="trimFraction"/>
    /// fraction of values. Falls back to normal average when there are too few values to trim safely.
    /// </summary>
    private static double? ComputeTrimmedMean(double[] sortedValues, double trimFraction)
    {
        if (sortedValues.Length == 0)
        {
            return null;
        }

        var trimCount = (int)Math.Floor(sortedValues.Length * trimFraction);

        // Need at least 2 values after trimming, or fall back to normal average
        if (sortedValues.Length - 2 * trimCount < 2)
        {
            return sortedValues.Average();
        }

        var trimmed = sortedValues
            .Skip(trimCount)
            .Take(sortedValues.Length - 2 * trimCount)
            .ToArray();

        return trimmed.Length > 0 ? trimmed.Average() : sortedValues.Average();
    }

    /// <summary>
    /// Computes median absolute deviation from the median.
    /// </summary>
    private static double? ComputeMad(double[] sortedValues, double median)
    {
        if (sortedValues.Length == 0)
        {
            return null;
        }

        var absoluteDeviations = sortedValues
            .Select(value => Math.Abs(value - median))
            .OrderBy(dev => dev)
            .ToArray();

        return ReadMedian(absoluteDeviations);
    }

    /// <summary>
    /// Computes jitter ratio: (P95 - P50) / P50.
    /// Returns null when P50 is null or close to zero.
    /// </summary>
    private static double? ComputeJitterRatio(double? p95, double? p50)
    {
        if (!p50.HasValue || Math.Abs(p50.Value) < P50Epsilon)
        {
            return null;
        }

        if (!p95.HasValue)
        {
            return null;
        }

        return (p95.Value - p50.Value) / p50.Value;
    }

    /// <summary>
    /// Computes outlier count and percentage using robust z-score.
    /// robustZ = 0.6745 * (x - median) / MAD.
    /// Values with |robustZ| > 3.5 are outliers.
    /// Returns (0, null) when MAD is null or close to zero.
    /// </summary>
    private static (int? count, double? percent) ComputeOutliers(
        double[] sortedValues, double median, double? mad)
    {
        if (sortedValues.Length == 0)
        {
            return (null, null);
        }

        if (!mad.HasValue || Math.Abs(mad.Value) < MadEpsilon)
        {
            return (0, 0.0);
        }

        var outlierCount = sortedValues.Count(value =>
            Math.Abs(RobustZScale * (value - median) / mad.Value) > OutlierZScoreThreshold);

        var outlierPercent = (double)outlierCount / sortedValues.Length * 100.0;
        return (outlierCount, outlierPercent);
    }
}
