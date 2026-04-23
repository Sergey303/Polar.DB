using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Renders inline SVG charts for the experiment HTML page.
/// Produces static SVG with tooltips, axes, and legends.
/// No external dependencies, no JavaScript.
/// </summary>
internal static class ChartRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Renders a history chart: elapsed median over time, one line per engine.
    /// </summary>
    public static string RenderHistoryChart(
        IReadOnlyList<ComparisonSnapshot> snapshots,
        IReadOnlyList<string> engineKeys)
    {
        var chartWidth = 940.0;
        var chartHeight = 340.0;
        var marginLeft = 72.0;
        var marginRight = 24.0;
        var marginTop = 24.0;
        var marginBottom = 78.0;
        var plotWidth = chartWidth - marginLeft - marginRight;
        var plotHeight = chartHeight - marginTop - marginBottom;

        var values = snapshots
            .SelectMany(snapshot => snapshot.EngineSeries.Select(series => series.ElapsedMs.Median))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (values.Length == 0)
        {
            return "<p class=\"muted\">History chart: no elapsed median values.</p>";
        }

        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1e-9)
        {
            min -= Math.Abs(min) * 0.1 + 1.0;
            max += Math.Abs(max) * 0.1 + 1.0;
        }
        else
        {
            var pad = (max - min) * 0.08;
            min -= pad;
            max += pad;
        }

        var xStep = snapshots.Count > 1 ? plotWidth / (snapshots.Count - 1) : 0.0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<svg class=\"chart\" viewBox=\"0 0 {chartWidth.ToString("0.###", Invariant)} {chartHeight.ToString("0.###", Invariant)}\" role=\"img\" aria-label=\"History elapsed median chart\">");
        sb.AppendLine("  <rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine("  <text x=\"18\" y=\"20\" font-size=\"14\" fill=\"#20252b\" font-weight=\"650\">History: elapsed median (ms) by series</text>");

        const int ticks = 5;
        for (var i = 0; i <= ticks; i++)
        {
            var ratio = i / (double)ticks;
            var y = marginTop + plotHeight - ratio * plotHeight;
            var value = min + ratio * (max - min);
            sb.AppendLine($"  <line x1=\"{marginLeft.ToString("0.###", Invariant)}\" y1=\"{y.ToString("0.###", Invariant)}\" x2=\"{(marginLeft + plotWidth).ToString("0.###", Invariant)}\" y2=\"{y.ToString("0.###", Invariant)}\" stroke=\"#e6eaf0\" stroke-width=\"1\" />");
            sb.AppendLine($"  <text x=\"{(marginLeft - 8).ToString("0.###", Invariant)}\" y=\"{(y + 4).ToString("0.###", Invariant)}\" text-anchor=\"end\" font-size=\"11\" fill=\"#66717f\">{NumberFormatter.HtmlEncode(NumberFormatter.FormatMillisecondsAxis(value))}</text>");
        }

        sb.AppendLine($"  <line x1=\"{marginLeft.ToString("0.###", Invariant)}\" y1=\"{marginTop.ToString("0.###", Invariant)}\" x2=\"{marginLeft.ToString("0.###", Invariant)}\" y2=\"{(marginTop + plotHeight).ToString("0.###", Invariant)}\" stroke=\"#9ca7b5\" stroke-width=\"1.2\" />");
        sb.AppendLine($"  <line x1=\"{marginLeft.ToString("0.###", Invariant)}\" y1=\"{(marginTop + plotHeight).ToString("0.###", Invariant)}\" x2=\"{(marginLeft + plotWidth).ToString("0.###", Invariant)}\" y2=\"{(marginTop + plotHeight).ToString("0.###", Invariant)}\" stroke=\"#9ca7b5\" stroke-width=\"1.2\" />");

        foreach (var engineKey in engineKeys)
        {
            var color = EngineColor(engineKey);
            var points = new List<(double X, double Y, ComparisonSnapshot Snapshot, double Value)>();
            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                var entry = snapshot.EngineSeries.FirstOrDefault(item =>
                    item.EngineKey.Equals(engineKey, StringComparison.OrdinalIgnoreCase));
                if (entry?.ElapsedMs.Median is not double median)
                {
                    continue;
                }

                var x = marginLeft + (snapshots.Count > 1 ? i * xStep : plotWidth * 0.5);
                var y = marginTop + plotHeight - (median - min) / (max - min) * plotHeight;
                points.Add((x, y, snapshot, median));
            }

            if (points.Count == 0)
            {
                continue;
            }

            var polyline = string.Join(" ", points.Select(point =>
                point.X.ToString("0.###", Invariant) + "," + point.Y.ToString("0.###", Invariant)));
            sb.AppendLine($"  <polyline points=\"{polyline}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2.6\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
            foreach (var point in points)
            {
                var tooltip = $"{engineKey} | {point.Snapshot.ComparisonSetId ?? "legacy"} | {point.Snapshot.SnapshotTimestampUtc:yyyy-MM-dd HH:mm:ss} UTC | median: {point.Value.ToString("0.###############", Invariant)} ms";
                sb.AppendLine($"  <circle cx=\"{point.X.ToString("0.###", Invariant)}\" cy=\"{point.Y.ToString("0.###", Invariant)}\" r=\"3.8\" fill=\"{color}\" stroke=\"#ffffff\" stroke-width=\"1\">");
                sb.AppendLine($"    <title>{NumberFormatter.HtmlEncode(tooltip)}</title>");
                sb.AppendLine("  </circle>");
            }
        }

        var labelStep = Math.Max(1, snapshots.Count / 6);
        for (var i = 0; i < snapshots.Count; i += labelStep)
        {
            var snapshot = snapshots[i];
            var x = marginLeft + (snapshots.Count > 1 ? i * xStep : plotWidth * 0.5);
            var label = SnapshotLabel(snapshot, maxChars: 14);
            sb.AppendLine($"  <text x=\"{x.ToString("0.###", Invariant)}\" y=\"{(marginTop + plotHeight + 18).ToString("0.###", Invariant)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#66717f\">{NumberFormatter.HtmlEncode(label)}</text>");
        }

        var legendX = marginLeft;
        var legendY = chartHeight - 24.0;
        foreach (var engine in engineKeys)
        {
            sb.AppendLine($"  <rect x=\"{legendX.ToString("0.###", Invariant)}\" y=\"{(legendY - 9).ToString("0.###", Invariant)}\" width=\"12\" height=\"12\" fill=\"{EngineColor(engine)}\" rx=\"2\" />");
            sb.AppendLine($"  <text x=\"{(legendX + 16).ToString("0.###", Invariant)}\" y=\"{legendY.ToString("0.###", Invariant)}\" font-size=\"11\" fill=\"#4e5a68\">{NumberFormatter.HtmlEncode(engine)}</text>");
            legendX += 130;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Renders a grouped bar chart for phase breakdown or artifact sizes.
    /// </summary>
    public static string RenderGroupedBarChart(
        string title,
        IReadOnlyList<string> categories,
        IReadOnlyList<ChartSeries> series,
        string unit,
        Func<double, string> axisFormatter)
    {
        var values = series
            .SelectMany(item => item.Values)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        if (values.Length == 0)
        {
            return $"<p class=\"muted\">{NumberFormatter.HtmlEncode(title)}: no data.</p>";
        }

        var chartWidth = 940.0;
        var chartHeight = 340.0;
        var marginLeft = 72.0;
        var marginRight = 20.0;
        var marginTop = 24.0;
        var marginBottom = 74.0;
        var plotWidth = chartWidth - marginLeft - marginRight;
        var plotHeight = chartHeight - marginTop - marginBottom;

        var max = values.Max();
        if (max <= 0)
        {
            max = 1.0;
        }
        var top = max * 1.1;
        var categoryWidth = plotWidth / Math.Max(1, categories.Count);
        var clusterWidth = categoryWidth * 0.72;
        var perSeriesWidth = clusterWidth / Math.Max(1, series.Count);
        var barWidth = Math.Max(8.0, perSeriesWidth * 0.76);
        var leftPadInCluster = (clusterWidth - perSeriesWidth * series.Count) * 0.5;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<svg class=\"chart\" viewBox=\"0 0 {chartWidth.ToString("0.###", Invariant)} {chartHeight.ToString("0.###", Invariant)}\" role=\"img\" aria-label=\"{NumberFormatter.HtmlEncode(title)}\">");
        sb.AppendLine("  <rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine($"  <text x=\"18\" y=\"20\" font-size=\"14\" fill=\"#20252b\" font-weight=\"650\">{NumberFormatter.HtmlEncode(title)}</text>");

        const int ticks = 5;
        for (var i = 0; i <= ticks; i++)
        {
            var ratio = i / (double)ticks;
            var y = marginTop + plotHeight - ratio * plotHeight;
            var value = ratio * top;
            sb.AppendLine($"  <line x1=\"{marginLeft.ToString("0.###", Invariant)}\" y1=\"{y.ToString("0.###", Invariant)}\" x2=\"{(marginLeft + plotWidth).ToString("0.###", Invariant)}\" y2=\"{y.ToString("0.###", Invariant)}\" stroke=\"#e6eaf0\" stroke-width=\"1\" />");
            sb.AppendLine($"  <text x=\"{(marginLeft - 8).ToString("0.###", Invariant)}\" y=\"{(y + 4).ToString("0.###", Invariant)}\" text-anchor=\"end\" font-size=\"11\" fill=\"#66717f\">{NumberFormatter.HtmlEncode(axisFormatter(value))}</text>");
        }

        sb.AppendLine($"  <line x1=\"{marginLeft.ToString("0.###", Invariant)}\" y1=\"{marginTop.ToString("0.###", Invariant)}\" x2=\"{marginLeft.ToString("0.###", Invariant)}\" y2=\"{(marginTop + plotHeight).ToString("0.###", Invariant)}\" stroke=\"#9ca7b5\" stroke-width=\"1.2\" />");
        sb.AppendLine($"  <line x1=\"{marginLeft.ToString("0.###", Invariant)}\" y1=\"{(marginTop + plotHeight).ToString("0.###", Invariant)}\" x2=\"{(marginLeft + plotWidth).ToString("0.###", Invariant)}\" y2=\"{(marginTop + plotHeight).ToString("0.###", Invariant)}\" stroke=\"#9ca7b5\" stroke-width=\"1.2\" />");

        for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            var groupStart = marginLeft + categoryIndex * categoryWidth + (categoryWidth - clusterWidth) * 0.5 + leftPadInCluster;
            for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
            {
                var value = series[seriesIndex].Values.ElementAtOrDefault(categoryIndex);
                if (value is not double rawValue || rawValue < 0)
                {
                    continue;
                }

                var barHeight = rawValue / top * plotHeight;
                var x = groupStart + seriesIndex * perSeriesWidth + (perSeriesWidth - barWidth) * 0.5;
                var y = marginTop + plotHeight - barHeight;
                var tooltip =
                    $"{series[seriesIndex].Name} | {categories[categoryIndex]} | {rawValue.ToString("0.###############", Invariant)} {unit}";
                sb.AppendLine($"  <rect x=\"{x.ToString("0.###", Invariant)}\" y=\"{y.ToString("0.###", Invariant)}\" width=\"{barWidth.ToString("0.###", Invariant)}\" height=\"{barHeight.ToString("0.###", Invariant)}\" fill=\"{series[seriesIndex].Color}\" rx=\"2\">");
                sb.AppendLine($"    <title>{NumberFormatter.HtmlEncode(tooltip)}</title>");
                sb.AppendLine("  </rect>");
            }

            var labelX = marginLeft + categoryIndex * categoryWidth + categoryWidth * 0.5;
            sb.AppendLine($"  <text x=\"{labelX.ToString("0.###", Invariant)}\" y=\"{(marginTop + plotHeight + 18).ToString("0.###", Invariant)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#66717f\">{NumberFormatter.HtmlEncode(categories[categoryIndex])}</text>");
        }

        var legendX = marginLeft;
        var legendY = chartHeight - 24.0;
        foreach (var item in series)
        {
            sb.AppendLine($"  <rect x=\"{legendX.ToString("0.###", Invariant)}\" y=\"{(legendY - 9).ToString("0.###", Invariant)}\" width=\"12\" height=\"12\" fill=\"{item.Color}\" rx=\"2\" />");
            sb.AppendLine($"  <text x=\"{(legendX + 16).ToString("0.###", Invariant)}\" y=\"{legendY.ToString("0.###", Invariant)}\" font-size=\"11\" fill=\"#4e5a68\">{NumberFormatter.HtmlEncode(item.Name)}</text>");
            legendX += 130;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Returns a stable color for a given target key.
    /// Target keys like "polar-db-current", "polar-db-2.1.1" map to the polar-db color.
    /// </summary>
    public static string EngineColor(string engineKey)
    {
        var normalized = engineKey.ToLowerInvariant();
        // Map target keys to engine family colors.
        if (normalized.StartsWith("polar-db", StringComparison.OrdinalIgnoreCase))
        {
            return "#0f6f9f";
        }

        return normalized switch
        {
            "sqlite" => "#a04f15",
            "synthetic" => "#5c3ea6",
            _ => "#2d7b63"
        };
    }

    /// <summary>
    /// Builds a short label for a snapshot (comparison set id or truncated timestamp).
    /// </summary>
    private static string SnapshotLabel(ComparisonSnapshot snapshot, int maxChars)
    {
        var label = snapshot.ComparisonSetId;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = snapshot.SnapshotTimestampUtc.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        if (label!.Length <= maxChars)
        {
            return label;
        }

        return label[..(maxChars - 3)] + "...";
    }
}

/// <summary>
/// One data series for a grouped bar chart.
/// </summary>
internal sealed record ChartSeries(string Name, string Color, IReadOnlyList<double?> Values);
