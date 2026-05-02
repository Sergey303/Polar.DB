#nullable enable
using System;
using System.Linq;
using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

internal static partial class HtmlSectionRenderer
{
    public static void AppendLatestEnginesSection(StringBuilder sb, ExperimentIndexModel model)
    {
        var latestEngines = ReadPath(model, "LatestEngines");
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Latest Target Comparison</h2>");

        if (latestEngines is null)
        {
            sb.AppendLine("  <p class=\"muted\">Comparison artifact <code>comparisons/latest-engines.json</code> is not available yet.</p>");
            sb.AppendLine("</section>");
            return;
        }

        var enabled = ReadBool(latestEngines, "Enabled") ?? false;
        sb.AppendLine("  <p>Status: <span class=\"" + (enabled ? "status-on" : "status-off") + "\">" + (enabled ? "enabled" : "disabled") + "</span></p>");

        var snapshot = ReadPath(latestEngines, "Snapshot");
        if (snapshot is null)
        {
            sb.AppendLine("  <p class=\"muted\">No complete successful measured series is currently available for all configured targets.</p>");
            AppendNotes(sb, ReadPath(latestEngines, "Notes"));
            sb.AppendLine("</section>");
            return;
        }

        var engines = Enumerate(ReadPath(snapshot, "EngineSeries"))
            .OrderBy(x => ReadString(x, "EngineKey"), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        sb.AppendLine("  <div class=\"kpis\">");
        AppendKpi(sb, "Comparison set", ReadString(snapshot, "ComparisonSetId") ?? "legacy/latest");
        AppendKpi(sb, "Timestamp", ReadString(snapshot, "SnapshotTimestampUtc") ?? "n/a");
        AppendKpi(sb, "Dataset", ReadString(snapshot, "DatasetProfileKey") ?? "mixed");
        AppendKpi(sb, "Fairness", ReadString(snapshot, "FairnessProfileKey") ?? "mixed");
        sb.AppendLine("  </div>");

        AppendLatestStatusTable(sb, engines);
        AppendLatestTimingTable(sb, engines);
        AppendLatestStabilityTable(sb, engines);
        AppendLatestStorageTable(sb, engines);
        AppendNotes(sb, ReadPath(latestEngines, "Notes"));
        AppendExpectations(sb, ReadPath(latestEngines, "DerivedExpectations"));
        sb.AppendLine("</section>");
    }

    private static void AppendKpi(StringBuilder sb, string label, string value)
    {
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">" + H(label) + "</span><span class=\"val mono\">" + H(value) + "</span></div>");
    }

    private static void AppendLatestStatusTable(StringBuilder sb, object[] engines)
    {
        sb.AppendLine("  <h3>Run status</h3>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th><th>Measured</th><th>Technical</th><th>Semantic</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in engines)
        {
            var engineKey = ReadString(engine, "EngineKey") ?? "unknown";
            var measured = ReadInt(engine, "MeasuredRunCount") ?? 0;
            var technical = ReadInt(engine, "TechnicalSuccessCount") ?? 0;
            var semantic = ReadInt(engine, "SemanticSuccessCount") ?? 0;
            var semanticEvaluated = ReadInt(engine, "SemanticEvaluatedCount") ?? measured;
            var ok = measured > 0 && technical == measured && (semanticEvaluated == 0 || semantic == semanticEvaluated);

            sb.AppendLine("      <tr class=\"" + (ok ? "" : "warn") + "\">");
            sb.AppendLine("        <td>" + Code(engineKey) + "</td>");
            sb.AppendLine("        <td>" + measured + "</td>");
            sb.AppendLine("        <td>" + technical + "/" + measured + "</td>");
            sb.AppendLine("        <td>" + semantic + "/" + semanticEvaluated + "</td>");
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void AppendLatestTimingTable(StringBuilder sb, object[] engines)
    {
        var columns = new[]
        {
            new TimingColumn("Elapsed", "ElapsedMs", false),
            new TimingColumn("Load", "LoadMs", false),
            new TimingColumn("Build", "BuildMs", false),
            new TimingColumn("Reopen", "ReopenMs", false),
            new TimingColumn("Index-only lookup", "indexOnlyLookupMs", true),
            new TimingColumn("Materialized lookup", "materializedLookupMs", true),
            new TimingColumn("Lookup", "LookupMs", false)
        };

        var visible = columns
            .Where(col => engines.Any(engine => GetMetricTm(engine, col.MetricKey, col.FromMetricsDictionary).HasValue || GetMetricP95(engine, col.MetricKey, col.FromMetricsDictionary).HasValue))
            .ToArray();

        sb.AppendLine("  <h3>Timing</h3>");
        sb.AppendLine("  <p class=\"muted small\">tm is trimmed mean without outliers. Missing metrics render as <code>N/A</code>, not as zero. Lower is better.</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th>Target</th>");
        foreach (var column in visible)
        {
            sb.AppendLine("        <th>" + H(column.Title) + " tm</th>");
            if (engines.Any(engine => GetMetricP95(engine, column.MetricKey, column.FromMetricsDictionary).HasValue))
            {
                sb.AppendLine("        <th>" + H(column.Title) + " p95</th>");
            }
        }
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");

        foreach (var engine in engines)
        {
            var engineKey = ReadString(engine, "EngineKey") ?? "unknown";
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + Code(engineKey) + "</td>");
            foreach (var column in visible)
            {
                var p95Visible = engines.Any(e => GetMetricP95(e, column.MetricKey, column.FromMetricsDictionary).HasValue);
                var tmMin = MinOrNull(engines.Select(e => GetMetricTm(e, column.MetricKey, column.FromMetricsDictionary)));
                var p95Min = MinOrNull(engines.Select(e => GetMetricP95(e, column.MetricKey, column.FromMetricsDictionary)));
                sb.AppendLine(FormatMetricCell(GetMetricTm(engine, column.MetricKey, column.FromMetricsDictionary), tmMin, MetricKind.Milliseconds));
                if (p95Visible) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, column.MetricKey, column.FromMetricsDictionary), p95Min, MetricKind.Milliseconds));
            }
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private readonly struct TimingColumn
    {
        public TimingColumn(string title, string metricKey, bool fromMetricsDictionary)
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
