using System.Globalization;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Shared formatting helpers for markdown and CSV report text.
/// </summary>
internal static class ReportFormatting
{
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
