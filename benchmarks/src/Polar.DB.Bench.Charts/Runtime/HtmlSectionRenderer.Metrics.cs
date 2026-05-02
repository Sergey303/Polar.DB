#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Polar.DB.Bench.Charts.Runtime;

internal static partial class HtmlSectionRenderer
{
    private static object? GetMetricStats(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        if (engine is null) return null;

        if (!fromMetricsDictionary)
        {
            var direct = GetProperty(engine, metricKey);
            if (direct is not null) return direct;
        }

        var metrics = GetProperty(engine, "Metrics");
        foreach (var entry in Enumerate(metrics))
        {
            var key = ReadString(entry, "Key");
            if (string.Equals(key, metricKey, StringComparison.OrdinalIgnoreCase))
            {
                return GetProperty(entry, "Value");
            }
        }

        // Some legacy models expose metric names as direct properties only.
        return fromMetricsDictionary ? null : GetProperty(engine, metricKey);
    }

    private static double? ReadDoubleStat(object? stats, string name)
    {
        var value = GetProperty(stats, name);
        if (value is null) return null;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is decimal m) return (double)m;
        if (value is int i) return i;
        if (value is long l) return l;
        if (double.TryParse(Convert.ToString(value, Invariant), System.Globalization.NumberStyles.Float, Invariant, out var parsed)) return parsed;
        return null;
    }

    private static double? GetMetricTm(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        var stats = GetMetricStats(engine, metricKey, fromMetricsDictionary);
        return ReadDoubleStat(stats, "TrimmedMean10")
            ?? ReadDoubleStat(stats, "Average")
            ?? ReadDoubleStat(stats, "Median")
            ?? ReadDoubleStat(stats, "P50")
            ?? ReadDoubleStat(stats, "Min");
    }

    private static double? GetMetricP50(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        var stats = GetMetricStats(engine, metricKey, fromMetricsDictionary);
        return ReadDoubleStat(stats, "P50")
            ?? ReadDoubleStat(stats, "Median")
            ?? ReadDoubleStat(stats, "Average")
            ?? ReadDoubleStat(stats, "Min");
    }

    private static double? GetMetricP95(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        return ReadDoubleStat(GetMetricStats(engine, metricKey, fromMetricsDictionary), "P95");
    }

    private static double? GetMetricP99(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        return ReadDoubleStat(GetMetricStats(engine, metricKey, fromMetricsDictionary), "P99");
    }

    private static double? GetMetricMad(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        return ReadDoubleStat(GetMetricStats(engine, metricKey, fromMetricsDictionary), "Mad");
    }

    private static double? GetMetricJitterRatio(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        return ReadDoubleStat(GetMetricStats(engine, metricKey, fromMetricsDictionary), "JitterRatio");
    }

    private static int? GetMetricOutlierCount(object? engine, string metricKey, bool fromMetricsDictionary = false)
    {
        var value = GetProperty(GetMetricStats(engine, metricKey, fromMetricsDictionary), "OutlierCount");
        if (value is null) return null;
        if (value is int i) return i;
        if (value is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
        if (int.TryParse(Convert.ToString(value, Invariant), out var parsed)) return parsed;
        return null;
    }

    private static void AppendNotes(StringBuilder sb, object? notes)
    {
        var items = Enumerate(notes).Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (items.Length == 0) return;

        sb.AppendLine("  <h3>Notes</h3>");
        sb.AppendLine("  <ul>");
        foreach (var note in items) sb.AppendLine("    <li>" + H(note) + "</li>");
        sb.AppendLine("  </ul>");
    }

    private static void AppendExpectations(StringBuilder sb, object? expectations)
    {
        var items = Enumerate(expectations).Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (items.Length == 0) return;

        sb.AppendLine("  <h3>Derived expectations</h3>");
        sb.AppendLine("  <ul>");
        foreach (var item in items) sb.AppendLine("    <li>" + H(item) + "</li>");
        sb.AppendLine("  </ul>");
    }

    private static string DescribeRuntimeSemantics(string targetKey, object? targetSpec)
    {
        var engine = ReadString(targetSpec, "Engine") ?? "unknown";
        var nuget = ReadString(targetSpec, "Nuget");

        if (!string.IsNullOrWhiteSpace(nuget)) return engine + " pinned NuGet " + nuget;
        if (targetKey.Contains("current", StringComparison.OrdinalIgnoreCase)) return engine + " current source";
        if (engine.Equals("sqlite", StringComparison.OrdinalIgnoreCase)) return "SQLite runtime used by the benchmark adapter";
        return engine + " runtime";
    }
}
