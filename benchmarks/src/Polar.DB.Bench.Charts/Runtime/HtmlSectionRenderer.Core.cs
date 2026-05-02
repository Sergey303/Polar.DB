#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Builds HTML sections for the experiment index page.
/// Split replacement for the former monolithic HtmlSectionRenderer.cs.
/// Delete the old HtmlSectionRenderer.cs after adding these files.
/// </summary>
internal static partial class HtmlSectionRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private enum MetricKind
    {
        Milliseconds,
        Bytes,
        General
    }

    private static string H(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private static string Code(string? value) => "<code>" + H(value ?? "n/a") + "</code>";

    private static object? GetProperty(object? value, string propertyName)
    {
        if (value is null || string.IsNullOrWhiteSpace(propertyName)) return null;
        var property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(value);
    }

    private static object? ReadPath(object? value, string path)
    {
        if (value is null || string.IsNullOrWhiteSpace(path)) return null;

        object? current = value;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            current = GetProperty(current, segment);
            if (current is null) return null;
        }

        return current;
    }

    private static string? ReadString(object? value, string path)
    {
        var result = ReadPath(value, path);
        return result switch
        {
            null => null,
            string text => text,
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", Invariant),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", Invariant),
            IFormattable formattable => formattable.ToString(null, Invariant),
            _ => result.ToString()
        };
    }

    private static int? ReadInt(object? value, string path)
    {
        var result = ReadPath(value, path);
        if (result is null) return null;
        if (result is int i) return i;
        if (result is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
        if (int.TryParse(Convert.ToString(result, Invariant), NumberStyles.Integer, Invariant, out var parsed)) return parsed;
        return null;
    }

    private static long? ReadLong(object? value, string path)
    {
        var result = ReadPath(value, path);
        if (result is null) return null;
        if (result is long l) return l;
        if (result is int i) return i;
        if (long.TryParse(Convert.ToString(result, Invariant), NumberStyles.Integer, Invariant, out var parsed)) return parsed;
        return null;
    }

    private static bool? ReadBool(object? value, string path)
    {
        var result = ReadPath(value, path);
        if (result is null) return null;
        if (result is bool b) return b;
        if (bool.TryParse(Convert.ToString(result, Invariant), out var parsed)) return parsed;
        return null;
    }

    private static IEnumerable<object> Enumerate(object? value)
    {
        if (value is null || value is string) yield break;
        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is not null) yield return item;
            }
        }
    }

    private static string FormatGeneralNumber(long? value)
    {
        return value.HasValue ? value.Value.ToString("N0", Invariant) : "n/a";
    }

    private static string FormatGeneralNumber(int? value)
    {
        return value.HasValue ? value.Value.ToString("N0", Invariant) : "n/a";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", Invariant);
    }

    private static string FormatMilliseconds(double? value)
    {
        if (!value.HasValue) return "N/A";
        var ms = value.Value;
        if (double.IsNaN(ms) || double.IsInfinity(ms)) return "N/A";
        if (Math.Abs(ms) < 0.001) return (ms * 1_000_000.0).ToString("0.###", Invariant) + " ns";
        if (Math.Abs(ms) < 1.0) return (ms * 1_000.0).ToString("0.###", Invariant) + " us";
        if (Math.Abs(ms) < 1000.0) return ms.ToString("0.###", Invariant) + " ms";
        return (ms / 1000.0).ToString("0.###", Invariant) + " s";
    }

    private static string FormatBytes(double? value)
    {
        if (!value.HasValue) return "N/A";
        var bytes = value.Value;
        if (double.IsNaN(bytes) || double.IsInfinity(bytes)) return "N/A";
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        var unit = 0;
        while (Math.Abs(bytes) >= 1024.0 && unit < units.Length - 1)
        {
            bytes /= 1024.0;
            unit++;
        }
        return bytes.ToString("0.###", Invariant) + " " + units[unit];
    }

    private static string FormatValue(double? value, MetricKind kind)
    {
        if (!value.HasValue) return "N/A";
        return kind switch
        {
            MetricKind.Milliseconds => FormatMilliseconds(value),
            MetricKind.Bytes => FormatBytes(value),
            _ => value.Value.ToString("0.###", Invariant)
        };
    }

    private static double? MinOrNull(IEnumerable<double?> values)
    {
        var materialized = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return materialized.Length == 0 ? null : materialized.Min();
    }

    private static bool IsBestMetricValue(double value, double? min)
    {
        return min.HasValue &&
               !double.IsNaN(value) &&
               !double.IsInfinity(value) &&
               Math.Abs(value - min.Value) < 1e-9;
    }

    private static string RatioText(double value, double? min)
    {
        if (!min.HasValue || double.IsNaN(value) || double.IsInfinity(value)) return string.Empty;
        if (IsBestMetricValue(value, min)) return " <span class=\"badge best\">best</span>";
        if (Math.Abs(min.Value) < 1e-12) return " <span class=\"badge metric-ratio\">+" + FormatNumber(value) + " over min</span>";
        return " <span class=\"badge metric-ratio\">×" + (value / min.Value).ToString("0.##", Invariant) + " min</span>";
    }

    private static string FormatMetricCell(double? value, double? min, MetricKind kind)
    {
        if (!value.HasValue)
        {
            return "        <td class=\"na metric-cell metric-na\"><span class=\"muted\">N/A</span></td>";
        }

        var isBest = IsBestMetricValue(value.Value, min);
        var cssClass = isBest
            ? "metric-cell metric-best best"
            : min.HasValue ? "metric-cell metric-compared" : "metric-cell";
        var formatted = FormatValue(value, kind);
        var raw = H(value.Value.ToString("R", Invariant));
        return "        <td class=\"" + cssClass + "\" title=\"" + raw + "\">" + formatted + RatioText(value.Value, min) + "</td>";
    }
}
