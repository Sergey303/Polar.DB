#nullable enable
using System;
using System.Linq;
using System.Text;

namespace Polar.DB.Bench.Charts.Runtime;

internal static partial class HtmlSectionRenderer
{
    private static void AppendLatestStorageTable(StringBuilder sb, object[] engines)
    {
        var columns = new[]
        {
            new StorageColumn("Total bytes", "TotalArtifactBytes"),
            new StorageColumn("Primary bytes", "PrimaryArtifactBytes"),
            new StorageColumn("Side bytes", "SideArtifactBytes")
        };

        if (!columns.Any(col => engines.Any(engine => GetMetricP50(engine, col.MetricKey).HasValue))) return;

        sb.AppendLine("  <h3>Storage footprint</h3>");
        sb.AppendLine("  <p class=\"muted small\">Storage uses p50 by default. Missing values render as <code>N/A</code>.</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th><th>Total bytes p50</th><th>Primary bytes p50</th><th>Side bytes p50</th></tr></thead>");
        sb.AppendLine("    <tbody>");

        foreach (var engine in engines)
        {
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + Code(ReadString(engine, "EngineKey") ?? "unknown") + "</td>");
            foreach (var column in columns)
            {
                var min = MinOrNull(engines.Select(e => GetMetricP50(e, column.MetricKey)));
                sb.AppendLine(FormatMetricCell(GetMetricP50(engine, column.MetricKey), min, MetricKind.Bytes));
            }
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void AppendLatestStabilityTable(StringBuilder sb, object[] engines)
    {
        var columns = new[]
        {
            new StabilityColumn("Elapsed", "ElapsedMs", false),
            new StabilityColumn("Load", "LoadMs", false),
            new StabilityColumn("Build", "BuildMs", false),
            new StabilityColumn("Reopen", "ReopenMs", false),
            new StabilityColumn("Index-only lookup", "indexOnlyLookupMs", true),
            new StabilityColumn("Materialized lookup", "materializedLookupMs", true),
            new StabilityColumn("Lookup", "LookupMs", false)
        };

        var visible = columns
            .Where(col => engines.Any(engine => GetMetricP95(engine, col.MetricKey, col.FromMetricsDictionary).HasValue || GetMetricP99(engine, col.MetricKey, col.FromMetricsDictionary).HasValue))
            .ToArray();

        if (visible.Length == 0) return;

        sb.AppendLine("  <h3>Stability</h3>");
        sb.AppendLine("  <p class=\"muted small\">p95/p99/tm/MAD/jitter%/outliers. Empty cells mean the statistic could not be computed.</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th>");
        foreach (var column in visible) sb.AppendLine("<th>" + H(column.Title) + " stability</th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in engines)
        {
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + Code(ReadString(engine, "EngineKey") ?? "unknown") + "</td>");
            foreach (var column in visible)
            {
                sb.AppendLine("        <td>" + FormatStabilityCell(engine, column.MetricKey, column.FromMetricsDictionary) + "</td>");
            }
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static string FormatStabilityCell(object engine, string metricKey, bool fromMetricsDictionary = false)
    {
        var pieces = new[]
        {
            ("p95", GetMetricP95(engine, metricKey, fromMetricsDictionary), MetricKind.Milliseconds),
            ("p99", GetMetricP99(engine, metricKey, fromMetricsDictionary), MetricKind.Milliseconds),
            ("tm", GetMetricTm(engine, metricKey, fromMetricsDictionary), MetricKind.Milliseconds),
            ("mad", GetMetricMad(engine, metricKey, fromMetricsDictionary), MetricKind.Milliseconds)
        };

        var text = string.Join(" ", pieces
            .Where(x => x.Item2.HasValue)
            .Select(x => x.Item1 + ":" + FormatValue(x.Item2, x.Item3)));

        var jitter = GetMetricJitterRatio(engine, metricKey, fromMetricsDictionary);
        if (jitter.HasValue) text += (text.Length > 0 ? " " : string.Empty) + "jit:" + (jitter.Value * 100.0).ToString("0.#", Invariant) + "%";

        var outliers = GetMetricOutlierCount(engine, metricKey, fromMetricsDictionary);
        if (outliers.HasValue) text += (text.Length > 0 ? " " : string.Empty) + "out:" + outliers.Value.ToString(Invariant);

        return string.IsNullOrWhiteSpace(text) ? "<span class=\"muted\">N/A</span>" : H(text);
    }

    private readonly struct StorageColumn
    {
        public StorageColumn(string title, string metricKey)
        {
            Title = title;
            MetricKey = metricKey;
        }

        public string Title { get; }
        public string MetricKey { get; }
    }

    private readonly struct StabilityColumn
    {
        public StabilityColumn(string title, string metricKey, bool fromMetricsDictionary)
        {
            Title = title;
            MetricKey = metricKey;
            FromMetricsDictionary = fromMetricsDictionary;
        }

        public string Title { get; }
        public string MetricKey { get; }
        public bool FromMetricsDictionary { get; }
    }
}
