using System.Globalization;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Shared formatting helpers for markdown and CSV report text.
/// </summary>
internal static class ReportFormatting
{
    public static string ExperimentDisplayName(string experimentKey)
    {
        return experimentKey switch
        {
            "persons-load-build-reopen-random-lookup" => "Imported persons load/build/reopen lookup",
            "persons-append-cycles-reopen-lookup" => "Persons append cycles with reopen lookup",
            _ => experimentKey
        };
    }

    /// <summary>
    /// Formats one series metric as <c>min/avg/median/max</c> string for markdown tables.
    /// </summary>
    public static string FormatStats(MetricSeriesStats stats)
    {
        var min = FormatNumber(stats.Min);
        var avg = FormatNumber(stats.Average);
        var med = FormatNumber(stats.Median);
        var max = FormatNumber(stats.Max);
        if (string.IsNullOrWhiteSpace(min) &&
            string.IsNullOrWhiteSpace(avg) &&
            string.IsNullOrWhiteSpace(med) &&
            string.IsNullOrWhiteSpace(max))
        {
            return string.Empty;
        }

        var value = $"{min}/{avg}/{med}/{max}";
        if (stats.MissingCount > 0)
        {
            value += $" [n={stats.Count - stats.MissingCount}/{stats.Count}]";
        }

        return value;
    }

    /// <summary>
    /// Formats one series metric as CSV fragment:
    /// count,missing,min,max,average,median.
    /// </summary>
    public static string FormatStatsCsv(MetricSeriesStats stats)
    {
        return
            $"{stats.Count}," +
            $"{stats.MissingCount}," +
            $"{Csv(FormatNumber(stats.Min))}," +
            $"{Csv(FormatNumber(stats.Max))}," +
            $"{Csv(FormatNumber(stats.Average))}," +
            $"{Csv(FormatNumber(stats.Median))}";
    }

    /// <summary>
    /// Formats stability fields as CSV fragment:
    /// p50,p95,p99,trimmedMean10,mad,jitterRatio,outlierCount,outlierPercent.
    /// </summary>
    public static string FormatStabilityCsv(MetricSeriesStats stats)
    {
        return
            $"{Csv(FormatNumber(stats.P50))}," +
            $"{Csv(FormatNumber(stats.P95))}," +
            $"{Csv(FormatNumber(stats.P99))}," +
            $"{Csv(FormatNumber(stats.TrimmedMean10))}," +
            $"{Csv(FormatNumber(stats.Mad))}," +
            $"{Csv(FormatJitterRatio(stats.JitterRatio))}," +
            $"{Csv(FormatOutlierCount(stats.OutlierCount))}," +
            $"{Csv(FormatOutlierPercent(stats.OutlierPercent))}";
    }

    /// <summary>
    /// Formats stability fields as a compact markdown string: p95/p99/trimmedMean10/mad/jitter%/outliers.
    /// </summary>
    public static string FormatStability(MetricSeriesStats stats)
    {
        var p95 = FormatNumber(stats.P95);
        var p99 = FormatNumber(stats.P99);
        var trimmed = FormatNumber(stats.TrimmedMean10);
        var mad = FormatNumber(stats.Mad);
        var jitter = FormatJitterRatio(stats.JitterRatio);
        var outliers = FormatOutlierCount(stats.OutlierCount);

        if (string.IsNullOrWhiteSpace(p95) &&
            string.IsNullOrWhiteSpace(p99) &&
            string.IsNullOrWhiteSpace(trimmed) &&
            string.IsNullOrWhiteSpace(mad) &&
            string.IsNullOrWhiteSpace(jitter) &&
            string.IsNullOrWhiteSpace(outliers))
        {
            return string.Empty;
        }

        return $"{p95}/{p99}/{trimmed}/{mad}/{jitter}/{outliers}";
    }

    /// <summary>
    /// Formats jitter ratio as percentage string, e.g. "12.3%".
    /// Returns empty string for null.
    /// </summary>
    public static string FormatJitterRatio(double? jitterRatio)
    {
        if (!jitterRatio.HasValue)
        {
            return string.Empty;
        }

        return (jitterRatio.Value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Formats outlier count. Returns "0" when there are valid samples and no outliers.
    /// Returns empty string when null.
    /// </summary>
    public static string FormatOutlierCount(int? outlierCount)
    {
        return outlierCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Formats outlier percent. Returns empty string when null.
    /// </summary>
    public static string FormatOutlierPercent(double? outlierPercent)
    {
        if (!outlierPercent.HasValue)
        {
            return string.Empty;
        }

        return outlierPercent.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Formats nullable numeric value using invariant culture.
    /// </summary>
    public static string FormatNumber(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>
    /// Formats nullable boolean value for report cells.
    /// </summary>
    public static string FormatBool(bool? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Escapes markdown table separators in cell text.
    /// </summary>
    public static string EscapeMarkdownCell(string value)
    {
        return value.Replace("|", "\\|");
    }

    /// <summary>
    /// Escapes a value for one CSV cell.
    /// </summary>
    public static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
