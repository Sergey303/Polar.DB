#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

internal static partial class HtmlSectionRenderer
{
    /// <summary>
    /// Renders optional thematic metric sections.
    /// This method is intentionally tolerant: when a metric is absent it renders N/A
    /// or skips an empty thematic block instead of turning missing values into zero.
    /// </summary>
    public static void AppendThematicMetricSections(StringBuilder sb, ExperimentIndexModel model)
    {
        var latestEngines = ReadPath(model, "LatestEngines");
        var snapshot = ReadPath(latestEngines, "Snapshot");
        if (snapshot is null)
        {
            return;
        }

        var engines = Enumerate(ReadPath(snapshot, "EngineSeries"))
            .OrderBy(x => ReadString(x, "EngineKey"), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (engines.Length == 0)
        {
            return;
        }

        var renderedAny = false;

        renderedAny |= AppendThematicMetricTable(
            sb,
            engines,
            "Lookup split phases",
            "Separates index-only lookup from materialized lookup. Missing metrics are shown as N/A, not as 0.",
            new[]
            {
                new ThematicMetricColumn("Index-only tm", "indexOnlyLookupMs", MetricKind.Milliseconds, StatKind.Tm),
                new ThematicMetricColumn("Index-only p95", "indexOnlyLookupMs", MetricKind.Milliseconds, StatKind.P95),
                new ThematicMetricColumn("Materialized tm", "materializedLookupMs", MetricKind.Milliseconds, StatKind.Tm),
                new ThematicMetricColumn("Materialized p95", "materializedLookupMs", MetricKind.Milliseconds, StatKind.P95),
                new ThematicMetricColumn("Compatibility lookup tm", "LookupMs", MetricKind.Milliseconds, StatKind.Tm, fromMetricsDictionary: false)
            });

        renderedAny |= AppendThematicMetricTable(
            sb,
            engines,
            "Lookup correctness counters",
            "Shows probe/hit/returned counters for the split lookup workloads.",
            new[]
            {
                new ThematicMetricColumn("Index probes", "indexOnlyProbeCount", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("Index hits", "indexOnlyProbeHits", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("Index offsets", "indexOnlyReturnedOffsets", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("Expected offsets", "indexOnlyExpectedOffsets", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("Materialized probes", "materializedProbeCount", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("Materialized hits", "materializedProbeHits", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("Returned rows", "materializedReturnedRows", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("Expected rows", "materializedExpectedRows", MetricKind.General, StatKind.Tm)
            });

        renderedAny |= AppendThematicMetricTable(
            sb,
            engines,
            "Legacy lookup aliases",
            "Compatibility aliases consumed by older analysis and charts. They should match the materialized phase for lookup-series experiments.",
            new[]
            {
                new ThematicMetricColumn("randomPointLookupMs tm", "randomPointLookupMs", MetricKind.Milliseconds, StatKind.Tm),
                new ThematicMetricColumn("lookupSeriesMs tm", "lookupSeriesMs", MetricKind.Milliseconds, StatKind.Tm),
                new ThematicMetricColumn("lookupHitCount", "lookupHitCount", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("lookupReturnedRows", "lookupReturnedRows", MetricKind.General, StatKind.Tm),
                new ThematicMetricColumn("lookupReturnedRowCount", "lookupReturnedRowCount", MetricKind.General, StatKind.Tm)
            });

        if (!renderedAny)
        {
            return;
        }
    }

    private static bool AppendThematicMetricTable(
        StringBuilder sb,
        object[] engines,
        string title,
        string description,
        IReadOnlyList<ThematicMetricColumn> columns)
    {
        var visibleColumns = columns
            .Where(column => engines.Any(engine => ReadThematicMetric(engine, column).HasValue))
            .ToArray();

        if (visibleColumns.Length == 0)
        {
            return false;
        }

        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>" + H(title) + "</h2>");
        sb.AppendLine("  <p class=\"muted small\">" + H(description) + "</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th>Target</th>");
        foreach (var column in visibleColumns)
        {
            sb.AppendLine("        <th>" + H(column.Title) + "</th>");
        }
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");

        foreach (var engine in engines)
        {
            var engineKey = ReadString(engine, "EngineKey") ?? "unknown";
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + Code(engineKey) + "</td>");
            foreach (var column in visibleColumns)
            {
                var value = ReadThematicMetric(engine, column);
                var min = column.Kind == MetricKind.General
                    ? null
                    : MinOrNull(engines.Select(e => ReadThematicMetric(e, column)));
                sb.AppendLine(FormatMetricCell(value, min, column.Kind));
            }
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</section>");
        return true;
    }

    private static double? ReadThematicMetric(object engine, ThematicMetricColumn column)
    {
        var value = column.Stat switch
        {
            StatKind.P50 => GetMetricP50(engine, column.MetricKey, column.FromMetricsDictionary),
            StatKind.P95 => GetMetricP95(engine, column.MetricKey, column.FromMetricsDictionary),
            StatKind.P99 => GetMetricP99(engine, column.MetricKey, column.FromMetricsDictionary),
            _ => GetMetricTm(engine, column.MetricKey, column.FromMetricsDictionary)
        };

        // Defensive rendering for lookup-series split phases. A previous analysis/raw mix can
        // materialize an absent metric as 0 ms. That is misleading because 20k probes cannot
        // honestly take exactly zero time. Treat non-positive split timing values as missing.
        if (column.Kind == MetricKind.Milliseconds &&
            IsLookupSplitTimingMetric(column.MetricKey) &&
            (!value.HasValue || value.Value <= 0.0))
        {
            return null;
        }

        return value;
    }

    private static bool IsLookupSplitTimingMetric(string metricKey)
    {
        return metricKey.Equals("indexOnlyLookupMs", StringComparison.OrdinalIgnoreCase) ||
               metricKey.Equals("materializedLookupMs", StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct ThematicMetricColumn
    {
        public ThematicMetricColumn(
            string title,
            string metricKey,
            MetricKind kind,
            StatKind stat,
            bool fromMetricsDictionary = true)
        {
            Title = title;
            MetricKey = metricKey;
            Kind = kind;
            Stat = stat;
            FromMetricsDictionary = fromMetricsDictionary;
        }

        public string Title { get; }
        public string MetricKey { get; }
        public MetricKind Kind { get; }
        public StatKind Stat { get; }
        public bool FromMetricsDictionary { get; }
    }

    private enum StatKind
    {
        Tm,
        P50,
        P95,
        P99
    }
}
