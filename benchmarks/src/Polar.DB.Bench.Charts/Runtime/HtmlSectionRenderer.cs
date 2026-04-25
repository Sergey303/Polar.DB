using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Builds HTML sections for the experiment index page.
/// Each method renders one card/section of the page.
/// </summary>
internal static class HtmlSectionRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private enum MetricKind
    {
        Milliseconds,
        Bytes
    }

    /// <summary>
    /// Renders the page header with title, experiment key, description, and generation timestamp.
    /// </summary>
    public static void AppendHeader(System.Text.StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"header\">");
        sb.AppendLine($"  <h1>{NumberFormatter.HtmlEncode(model.Manifest.Title)}</h1>");
        sb.AppendLine($"  <p class=\"muted\">Experiment: <code>{NumberFormatter.HtmlEncode(model.Manifest.ExperimentKey)}</code></p>");
        if (!string.IsNullOrWhiteSpace(model.Manifest.Description))
        {
            sb.AppendLine($"  <p>{NumberFormatter.HtmlEncode(model.Manifest.Description!)}</p>");
        }

        sb.AppendLine("  <p class=\"meta\">");
        sb.AppendLine($"    Generated: <span class=\"mono\">{NumberFormatter.HtmlEncode(model.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", Invariant))}</span>.");
        sb.AppendLine("    Numbers use scientific display for large values; exact raw values are available in tooltip.");
        sb.AppendLine("    Ratio badges compare each metric cell to the minimum value in the same column.");
        sb.AppendLine("  </p>");
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Renders the identity section: dataset, workload, fairness, and targets tables.
    /// </summary>
    public static void AppendIdentitySection(System.Text.StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"grid\">");
        sb.AppendLine("  <article class=\"card\">");
        sb.AppendLine("    <h2>Dataset</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <tr><th>Profile</th><td><code>" + NumberFormatter.HtmlEncode(model.Manifest.Dataset.ProfileKey) + "</code></td></tr>");
        sb.AppendLine("      <tr><th>Count</th><td>" + NumberFormatter.FormatGeneralNumber(model.Manifest.Dataset.RecordCount) + "</td></tr>");
        sb.AppendLine("      <tr><th>Seed</th><td>" + (model.Manifest.Dataset.Seed?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Notes</th><td>" + NumberFormatter.HtmlEncode(model.Manifest.Dataset.Notes ?? string.Empty) + "</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </article>");

        sb.AppendLine("  <article class=\"card\">");
        sb.AppendLine("    <h2>Workload</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <tr><th>Type</th><td><code>" + NumberFormatter.HtmlEncode(model.Manifest.Workload.WorkloadKey) + "</code></td></tr>");
        sb.AppendLine("      <tr><th>Lookup</th><td>" + (model.Manifest.Workload.LookupCount?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Batches</th><td>" + (model.Manifest.Workload.BatchCount?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Batch size</th><td>" + (model.Manifest.Workload.BatchSize?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Notes</th><td>" + NumberFormatter.HtmlEncode(model.Manifest.Workload.Notes ?? string.Empty) + "</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </article>");

        sb.AppendLine("  <article class=\"card\">");
        sb.AppendLine("    <h2>Fairness</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <tr><th>Profile</th><td><code>" + NumberFormatter.HtmlEncode(model.Manifest.FairnessProfile.FairnessProfileKey) + "</code></td></tr>");
        sb.AppendLine("      <tr><th>Notes</th><td>" + NumberFormatter.HtmlEncode(model.Manifest.FairnessProfile.Notes ?? string.Empty) + "</td></tr>");
        sb.AppendLine("      <tr><th>Research</th><td>" + NumberFormatter.HtmlEncode(model.Manifest.ResearchQuestionId ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Hypothesis</th><td>" + NumberFormatter.HtmlEncode(model.Manifest.HypothesisId ?? "n/a") + "</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </article>");
        sb.AppendLine("</section>");

        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Targets</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th><th>Engine family</th><th>Spec</th><th>Runtime semantics</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var (targetKey, targetSpec) in model.Manifest.Targets.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var spec = string.IsNullOrWhiteSpace(targetSpec.Nuget)
                ? "{}"
                : "{ \"nuget\": \"" + NumberFormatter.HtmlEncode(targetSpec.Nuget!) + "\" }";
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(targetKey) + "</code></td>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(targetSpec.Engine) + "</code></td>");
            sb.AppendLine("        <td><code>" + spec + "</code></td>");
            sb.AppendLine("        <td>" + NumberFormatter.HtmlEncode(DescribeRuntimeSemantics(targetKey, targetSpec)) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Renders the latest engine comparison section with KPI cards, tables, and charts.
    /// </summary>
    public static void AppendLatestEnginesSection(System.Text.StringBuilder sb, ExperimentIndexModel model)
    {
        var latestEngines = model.LatestEngines;
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Latest Target Comparison</h2>");

        if (latestEngines is null)
        {
            sb.AppendLine("  <p class=\"muted\">Comparison artifact <code>comparisons/latest-engines.json</code> is not available yet.</p>");
            sb.AppendLine("</section>");
            return;
        }

        var statusClass = latestEngines.Enabled ? "status-on" : "status-off";
        var statusText = latestEngines.Enabled ? "enabled" : "disabled";
        sb.AppendLine($"  <p>Status: <span class=\"{statusClass}\">{statusText}</span></p>");

        if (latestEngines.Snapshot is null)
        {
            sb.AppendLine("  <p class=\"muted\">No complete successful measured series is currently available for all configured targets.</p>");
            AppendNotes(sb, latestEngines.Notes);
            sb.AppendLine("</section>");
            return;
        }

        var snapshot = latestEngines.Snapshot;
        var engines = snapshot.EngineSeries
            .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var colors = ChartRenderer.BuildEngineColorMap(engines.Select(item => item.EngineKey));

        sb.AppendLine("  <div class=\"kpis\">");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Comparison set</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.ComparisonSetId ?? "legacy/latest") + "</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Timestamp</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.SnapshotTimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", Invariant)) + " UTC</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Dataset</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.DatasetProfileKey ?? "mixed") + "</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Fairness</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.FairnessProfileKey ?? "mixed") + "</span></div>");
        sb.AppendLine("  </div>");

        AppendEngineLegend(sb, colors, engines.Select(item => item.EngineKey));
        AppendLatestStatusTable(sb, engines);
        AppendLatestTimingTable(sb, engines);
        AppendLatestStorageTable(sb, engines);

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(ChartRenderer.RenderGroupedBarChart(
            "Phase Breakdown (latest series, p50 ms)",
            new[] { "Load", "Build", "Reopen", "Lookup" },
            engines
                .Select(item => new ChartSeries(
                    item.EngineKey,
                    ResolveColor(colors, item.EngineKey),
                    new double?[]
                    {
                        GetMetricP50(item, "LoadMs"),
                        GetMetricP50(item, "BuildMs"),
                        GetMetricP50(item, "ReopenMs"),
                        GetMetricP50(item, "LookupMs")
                    }))
                .ToArray(),
            "ms",
            NumberFormatter.FormatMillisecondsAxis));
        sb.AppendLine("  </div>");

        if (engines.Any(item => GetMetricP95(item, "LoadMs").HasValue || GetMetricP95(item, "BuildMs").HasValue || GetMetricP95(item, "ReopenMs").HasValue || GetMetricP95(item, "LookupMs").HasValue))
        {
            sb.AppendLine("  <div class=\"chart-wrap\">");
            sb.AppendLine(ChartRenderer.RenderGroupedBarChart(
                "Phase Breakdown (latest series, p95 ms)",
                new[] { "Load", "Build", "Reopen", "Lookup" },
                engines
                    .Select(item => new ChartSeries(
                        item.EngineKey,
                        ResolveColor(colors, item.EngineKey),
                        new double?[]
                        {
                            GetMetricP95(item, "LoadMs"),
                            GetMetricP95(item, "BuildMs"),
                            GetMetricP95(item, "ReopenMs"),
                            GetMetricP95(item, "LookupMs")
                        }))
                    .ToArray(),
                "ms",
                NumberFormatter.FormatMillisecondsAxis));
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(ChartRenderer.RenderGroupedBarChart(
            "Artifact Sizes (latest series, p50 bytes)",
            new[] { "Primary", "Side", "Total" },
            engines
                .Select(item => new ChartSeries(
                    item.EngineKey,
                    ResolveColor(colors, item.EngineKey),
                    new double?[]
                    {
                        GetMetricP50(item, "PrimaryArtifactBytes"),
                        GetMetricP50(item, "SideArtifactBytes"),
                        GetMetricP50(item, "TotalArtifactBytes")
                    }))
                .ToArray(),
            "B",
            NumberFormatter.FormatBytesAxis));
        sb.AppendLine("  </div>");

        AppendNotes(sb, latestEngines.Notes);
        AppendExpectations(sb, latestEngines.DerivedExpectations);
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Renders the history section with table and history chart.
    /// </summary>
    public static void AppendHistorySection(System.Text.StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>History</h2>");

        var historyEnabled = model.Manifest.Compare.History;
        if (!historyEnabled)
        {
            sb.AppendLine("  <p class=\"status-off\">Disabled by experiment <code>compare.history</code> flag.</p>");
            sb.AppendLine("</section>");
            return;
        }

        var latestHistory = model.LatestHistory;
        if (latestHistory is null)
        {
            sb.AppendLine("  <p class=\"muted\">Comparison artifact <code>comparisons/latest-history.json</code> is not available yet.</p>");
            sb.AppendLine("</section>");
            return;
        }

        if (!latestHistory.Enabled)
        {
            sb.AppendLine("  <p class=\"status-off\">History artifact is currently marked disabled.</p>");
            AppendNotes(sb, latestHistory.Notes);
            sb.AppendLine("</section>");
            return;
        }

        var snapshots = latestHistory.Snapshots.OrderBy(item => item.SnapshotTimestampUtc).ToArray();
        if (snapshots.Length == 0)
        {
            sb.AppendLine("  <p class=\"muted\">No successful measured history snapshots were found.</p>");
            AppendNotes(sb, latestHistory.Notes);
            sb.AppendLine("</section>");
            return;
        }

        var engines = snapshots
            .SelectMany(item => item.EngineSeries.Select(series => series.EngineKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>#</th><th>Set</th><th>Timestamp</th>");
        foreach (var engine in engines)
        {
            sb.AppendLine("<th>Elapsed p50: <code>" + NumberFormatter.HtmlEncode(engine) + "</code></th>");
        }

        sb.AppendLine("</tr></thead>");
        sb.AppendLine("    <tbody>");
        for (var i = 0; i < snapshots.Length; i++)
        {
            var snapshot = snapshots[i];
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + (i + 1) + "</td>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(snapshot.ComparisonSetId ?? "legacy") + "</code></td>");
            sb.AppendLine("        <td class=\"mono\">" + NumberFormatter.HtmlEncode(snapshot.SnapshotTimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", Invariant)) + "</td>");
            foreach (var engine in engines)
            {
                var series = snapshot.EngineSeries.FirstOrDefault(item =>
                    item.EngineKey.Equals(engine, StringComparison.OrdinalIgnoreCase));
                sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(GetMetricP50(series, "ElapsedMs")) + "</td>");
            }

            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(ChartRenderer.RenderHistoryChart(snapshots, engines));
        sb.AppendLine("  </div>");

        AppendNotes(sb, latestHistory.Notes);
        AppendExpectations(sb, latestHistory.DerivedExpectations);
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Renders the cross-experiment context section.
    /// </summary>
    public static void AppendOtherExperimentsSection(System.Text.StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Context With Other Experiments</h2>");

        var otherEnabled = model.Manifest.Compare.OtherExperiments;
        if (!otherEnabled)
        {
            sb.AppendLine("  <p class=\"status-off\">Disabled by experiment <code>compare.otherExperiments</code> flag.</p>");
            sb.AppendLine("</section>");
            return;
        }

        var latestOther = model.LatestOtherExperiments;
        if (latestOther is null)
        {
            sb.AppendLine("  <p class=\"muted\">Comparison artifact <code>comparisons/latest-other-experiments.json</code> is not available yet.</p>");
            sb.AppendLine("</section>");
            return;
        }

        if (!latestOther.Enabled)
        {
            sb.AppendLine("  <p class=\"status-off\">Other-experiments artifact is currently marked disabled.</p>");
            AppendNotes(sb, latestOther.Notes);
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <p class=\"muted\">This section is context-only and does not claim strict apples-to-apples equivalence.</p>");
        var others = latestOther.OtherExperimentSnapshots
            .OrderBy(item => item.ExperimentKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SnapshotTimestampUtc)
            .ToArray();

        if (others.Length == 0)
        {
            sb.AppendLine("  <p class=\"muted\">No resolved external snapshots were found for auto-discovered experiments.</p>");
            AppendNotes(sb, latestOther.Notes);
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Experiment</th><th>Set</th><th>Timestamp</th><th>Dataset</th><th>Fairness</th><th>Elapsed p50 per target</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var snapshot in others)
        {
            var summary = string.Join("; ", snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.EngineKey}: {NumberFormatter.StripHtml(NumberFormatter.FormatMilliseconds(GetMetricP50(item, "ElapsedMs")))}"));
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(snapshot.ExperimentKey) + "</code></td>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(snapshot.ComparisonSetId ?? "legacy") + "</code></td>");
            sb.AppendLine("        <td class=\"mono\">" + NumberFormatter.HtmlEncode(snapshot.SnapshotTimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", Invariant)) + "</td>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(snapshot.DatasetProfileKey ?? "mixed") + "</code></td>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(snapshot.FairnessProfileKey ?? "mixed") + "</code></td>");
            sb.AppendLine("        <td>" + NumberFormatter.HtmlEncode(summary) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        AppendNotes(sb, latestOther.Notes);
        AppendExpectations(sb, latestOther.DerivedExpectations);
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Renders the machine-readable artifacts section with links to raw, analyzed, and comparison files.
    /// </summary>
    public static void AppendArtifactsSection(System.Text.StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Machine-Readable Artifacts</h2>");
        sb.AppendLine("  <p class=\"muted small\">These links point to raw facts and derived JSON artifacts inside this experiment folder.</p>");

        AppendArtifactList(sb, "Latest Raw Files", model.RawArtifacts, maxRows: 24);
        AppendArtifactList(sb, "Analyzed Files", model.AnalyzedArtifacts, maxRows: 48);
        AppendArtifactList(sb, "Comparison Files", model.ComparisonArtifacts, maxRows: 48);

        if (model.LocalAnalyzedSeries.Count > 0)
        {
            sb.AppendLine("  <h3>Local Analyzed Snapshot</h3>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead><tr><th>Target</th><th>Set</th><th>Elapsed p50</th><th>Total bytes p50</th><th>Measured</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in model.LocalAnalyzedSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(item.EngineKey) + "</code></td>");
                sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(item.ComparisonSetId ?? "legacy/latest") + "</code></td>");
                sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(GetMetricP50(item, "ElapsedMs")) + "</td>");
                sb.AppendLine("        <td>" + NumberFormatter.FormatBytes(GetMetricP50(item, "TotalArtifactBytes")) + "</td>");
                sb.AppendLine("        <td>" + item.MeasuredRunCount + "</td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        sb.AppendLine("</section>");
    }

    private static void AppendLatestStatusTable<T>(System.Text.StringBuilder sb, IReadOnlyList<T> engines)
    {
        sb.AppendLine("  <h3>Run status</h3>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th><th>Measured</th><th>Technical</th><th>Semantic</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in engines)
        {
            var engineKey = GetStringMember(engine, "EngineKey") ?? "unknown";
            var measured = GetIntMember(engine, "MeasuredRunCount") ?? 0;
            var technical = GetIntMember(engine, "TechnicalSuccessCount") ?? 0;
            var semantic = GetIntMember(engine, "SemanticSuccessCount") ?? 0;
            var semanticEvaluated = GetIntMember(engine, "SemanticEvaluatedCount") ?? measured;

            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(engineKey) + "</code></td>");
            sb.AppendLine("        <td>" + measured + "</td>");
            sb.AppendLine("        <td>" + technical + "/" + measured + "</td>");
            sb.AppendLine("        <td>" + semantic + "/" + semanticEvaluated + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void AppendLatestTimingTable<T>(System.Text.StringBuilder sb, IReadOnlyList<T> engines)
    {
        var showElapsedP95 = engines.Any(item => GetMetricP95(item, "ElapsedMs").HasValue);
        var showLoadP95 = engines.Any(item => GetMetricP95(item, "LoadMs").HasValue);
        var showBuildP95 = engines.Any(item => GetMetricP95(item, "BuildMs").HasValue);
        var showReopenP95 = engines.Any(item => GetMetricP95(item, "ReopenMs").HasValue);
        var showLookupP95 = engines.Any(item => GetMetricP95(item, "LookupMs").HasValue);

        var elapsedP50Min = MinOrNull(engines.Select(item => GetMetricP50(item, "ElapsedMs")));
        var elapsedP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "ElapsedMs")));
        var loadP50Min = MinOrNull(engines.Select(item => GetMetricP50(item, "LoadMs")));
        var loadP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "LoadMs")));
        var buildP50Min = MinOrNull(engines.Select(item => GetMetricP50(item, "BuildMs")));
        var buildP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "BuildMs")));
        var reopenP50Min = MinOrNull(engines.Select(item => GetMetricP50(item, "ReopenMs")));
        var reopenP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "ReopenMs")));
        var lookupP50Min = MinOrNull(engines.Select(item => GetMetricP50(item, "LookupMs")));
        var lookupP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "LookupMs")));

        sb.AppendLine("  <h3>Timing</h3>");
        sb.AppendLine("  <p class=\"muted small\">p50 is median. p95 columns appear when p95 values exist in comparison artifacts. Lower is better.</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th>Target</th><th>Elapsed p50</th>" + (showElapsedP95 ? "<th>Elapsed p95</th>" : string.Empty) + "<th>Load p50</th>" + (showLoadP95 ? "<th>Load p95</th>" : string.Empty) + "<th>Build p50</th>" + (showBuildP95 ? "<th>Build p95</th>" : string.Empty) + "<th>Reopen p50</th>" + (showReopenP95 ? "<th>Reopen p95</th>" : string.Empty) + "<th>Lookup p50</th>" + (showLookupP95 ? "<th>Lookup p95</th>" : string.Empty));
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in engines)
        {
            var engineKey = GetStringMember(engine, "EngineKey") ?? "unknown";

            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(engineKey) + "</code></td>");
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "ElapsedMs"), elapsedP50Min, MetricKind.Milliseconds));
            if (showElapsedP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "ElapsedMs"), elapsedP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "LoadMs"), loadP50Min, MetricKind.Milliseconds));
            if (showLoadP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "LoadMs"), loadP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "BuildMs"), buildP50Min, MetricKind.Milliseconds));
            if (showBuildP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "BuildMs"), buildP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "ReopenMs"), reopenP50Min, MetricKind.Milliseconds));
            if (showReopenP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "ReopenMs"), reopenP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "LookupMs"), lookupP50Min, MetricKind.Milliseconds));
            if (showLookupP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "LookupMs"), lookupP95Min, MetricKind.Milliseconds));
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void AppendLatestStorageTable<T>(System.Text.StringBuilder sb, IReadOnlyList<T> engines)
    {
        var totalMin = MinOrNull(engines.Select(item => GetMetricP50(item, "TotalArtifactBytes")));
        var primaryMin = MinOrNull(engines.Select(item => GetMetricP50(item, "PrimaryArtifactBytes")));
        var sideMin = MinOrNull(engines.Select(item => GetMetricP50(item, "SideArtifactBytes")));

        sb.AppendLine("  <h3>Storage footprint</h3>");
        sb.AppendLine("  <p class=\"muted small\">Storage uses p50 by default. Lower is better.</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th><th>Total bytes p50</th><th>Primary bytes p50</th><th>Side bytes p50</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in engines)
        {
            var engineKey = GetStringMember(engine, "EngineKey") ?? "unknown";

            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(engineKey) + "</code></td>");
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "TotalArtifactBytes"), totalMin, MetricKind.Bytes));
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "PrimaryArtifactBytes"), primaryMin, MetricKind.Bytes));
            sb.AppendLine(FormatMetricCell(GetMetricP50(engine, "SideArtifactBytes"), sideMin, MetricKind.Bytes));
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void AppendEngineLegend(System.Text.StringBuilder sb, IReadOnlyDictionary<string, string> colors, IEnumerable<string> engineKeys)
    {
        sb.AppendLine("  <div class=\"legend\" aria-label=\"Target color legend\">");
        foreach (var engineKey in engineKeys)
        {
            var color = ResolveColor(colors, engineKey);
            sb.AppendLine("    <span class=\"legend-item\"><span class=\"swatch\" style=\"background:" + NumberFormatter.HtmlEncode(color) + "\"></span><code>" + NumberFormatter.HtmlEncode(engineKey) + "</code></span>");
        }

        sb.AppendLine("  </div>");
    }

    private static string FormatMetricCell(double? value, double? min, MetricKind kind)
    {
        var main = kind == MetricKind.Bytes
            ? NumberFormatter.FormatBytes(value)
            : NumberFormatter.FormatMilliseconds(value);

        if (!value.HasValue)
        {
            return "        <td class=\"metric-cell\"><div class=\"metric-main\">" + main + "</div></td>";
        }

        var isBest = min.HasValue && AreSame(value.Value, min.Value);
        var ratio = FormatRatio(value.Value, min, kind);
        var css = isBest ? "metric-cell metric-best" : "metric-cell";
        return "        <td class=\"" + css + "\"><div class=\"metric-main\">" + main + "</div><div class=\"metric-ratio\">" + NumberFormatter.HtmlEncode(ratio) + "</div></td>";
    }

    private static string FormatRatio(double value, double? min, MetricKind kind)
    {
        if (!min.HasValue)
        {
            return string.Empty;
        }

        if (AreSame(value, min.Value))
        {
            return "best";
        }

        if (Math.Abs(min.Value) < 1e-12)
        {
            return "+" + FormatPlainMetric(value - min.Value, kind) + " over min";
        }

        var ratio = value / min.Value;
        return "×" + ratio.ToString("0.##", Invariant) + " min";
    }

    private static string FormatPlainMetric(double value, MetricKind kind)
    {
        if (kind == MetricKind.Bytes)
        {
            return NumberFormatter.FormatBinaryBytes(value);
        }

        if (Math.Abs(value) >= 1000.0)
        {
            return (value / 1000.0).ToString("0.###", Invariant) + " s";
        }

        return value.ToString("0.###", Invariant) + " ms";
    }

    private static double? MinOrNull(IEnumerable<double?> values)
    {
        var materialized = values
            .Where(value => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value))
            .Select(value => value!.Value)
            .ToArray();
        return materialized.Length == 0 ? null : materialized.Min();
    }

    private static bool AreSame(double left, double right)
    {
        var scale = Math.Max(1.0, Math.Max(Math.Abs(left), Math.Abs(right)));
        return Math.Abs(left - right) <= scale * 1e-9;
    }

    private static double? GetMetricP50(object? source, string metricName)
    {
        var stats = GetMemberValue(source, metricName);
        return GetOptionalMetricValue(
            stats,
            "Median",
            "P50",
            "P50Value",
            "Percentile50",
            "FiftiethPercentile",
            "P50Ms",
            "MedianMs",
            "ValueP50");
    }

    private static double? GetMetricP95(object? source, string metricName)
    {
        return GetP95(GetMemberValue(source, metricName));
    }

    private static string? GetStringMember(object? source, string memberName)
    {
        return GetMemberValue(source, memberName)?.ToString();
    }

    private static int? GetIntMember(object? source, string memberName)
    {
        var value = GetMemberValue(source, memberName);
        if (value is null)
        {
            return null;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is IConvertible convertible)
        {
            return Convert.ToInt32(convertible, Invariant);
        }

        return null;
    }

    private static object? GetMemberValue(object? source, string memberName)
    {
        if (source is null)
        {
            return null;
        }

        var sourceType = source.GetType();
        var property = sourceType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is not null)
        {
            return property.GetValue(source);
        }

        var field = sourceType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (field is not null)
        {
            return field.GetValue(source);
        }

        return null;
    }

    private static double? GetP95(object? stats)
    {
        return GetOptionalMetricValue(
            stats,
            "P95",
            "P95Value",
            "Percentile95",
            "NinetyFifthPercentile",
            "P95Ms",
            "ValueP95");
    }

    private static double? GetOptionalMetricValue(object? source, params string[] memberNames)
    {
        if (source is null)
        {
            return null;
        }

        var sourceType = source.GetType();
        foreach (var memberName in memberNames)
        {
            var property = sourceType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is not null)
            {
                return ConvertToNullableDouble(property.GetValue(source));
            }

            var field = sourceType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (field is not null)
            {
                return ConvertToNullableDouble(field.GetValue(source));
            }
        }

        return null;
    }

    private static double? ConvertToNullableDouble(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is double d)
        {
            return d;
        }


        if (value is IConvertible convertible)
        {
            return Convert.ToDouble(convertible, Invariant);
        }

        return null;
    }

    private static string ResolveColor(IReadOnlyDictionary<string, string> colors, string engineKey)
    {
        return colors.TryGetValue(engineKey, out var color)
            ? color
            : ChartRenderer.EngineColor(engineKey);
    }

    /// <summary>
    /// Renders a list of artifact file links in a table.
    /// </summary>
    private static void AppendArtifactList(
        System.Text.StringBuilder sb,
        string title,
        IReadOnlyList<ArtifactFileLink> files,
        int maxRows)
    {
        sb.AppendLine($"  <h3>{NumberFormatter.HtmlEncode(title)}</h3>");
        if (files.Count == 0)
        {
            sb.AppendLine("  <p class=\"muted\">No files found.</p>");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>File</th><th>Updated (UTC)</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var file in files.Take(maxRows))
        {
            var fileName = Path.GetFileName(file.RelativePath);
            var updated = file.LastWriteUtc is null
                ? "n/a"
                : NumberFormatter.HtmlEncode(file.LastWriteUtc.Value.ToString("yyyy-MM-dd HH:mm:ss", Invariant));
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><a href=\"./" + NumberFormatter.HtmlEncode(file.RelativePath.Replace('\\', '/')) + "\"><code>" + NumberFormatter.HtmlEncode(fileName) + "</code></a></td>");
            sb.AppendLine("        <td class=\"mono\">" + updated + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        if (files.Count > maxRows)
        {
            sb.AppendLine("  <p class=\"muted small\">Showing " + maxRows + " of " + files.Count + " files.</p>");
        }
    }

    /// <summary>
    /// Renders a notes list.
    /// </summary>
    public static void AppendNotes(System.Text.StringBuilder sb, IReadOnlyList<string>? notes)
    {
        if (notes is null || notes.Count == 0)
        {
            return;
        }

        sb.AppendLine("  <h3>Notes</h3>");
        sb.AppendLine("  <ul class=\"clean\">");
        foreach (var note in notes.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            sb.AppendLine("    <li>" + NumberFormatter.HtmlEncode(note) + "</li>");
        }

        sb.AppendLine("  </ul>");
    }

    /// <summary>
    /// Renders a derived expectations list.
    /// </summary>
    public static void AppendExpectations(System.Text.StringBuilder sb, IReadOnlyList<string>? expectations)
    {
        if (expectations is null || expectations.Count == 0)
        {
            return;
        }

        sb.AppendLine("  <h3>Derived Expectations</h3>");
        sb.AppendLine("  <ul class=\"clean\">");
        foreach (var expectation in expectations.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            sb.AppendLine("    <li>" + NumberFormatter.HtmlEncode(expectation) + "</li>");
        }

        sb.AppendLine("  </ul>");
    }

    /// <summary>
    /// Describes runtime semantics for a target spec in human-readable form.
    /// </summary>
    private static string DescribeRuntimeSemantics(string targetKey, ExperimentTargetSpec spec)
    {
        var hasNuget = !string.IsNullOrWhiteSpace(spec.Nuget);
        var isPolar = spec.Engine.Equals("polar-db", StringComparison.OrdinalIgnoreCase);

        if (isPolar && !hasNuget)
        {
            return "Current source from repository.";
        }

        if (isPolar && hasNuget)
        {
            return $"Pinned NuGet version {spec.Nuget}.";
        }

        if (!isPolar && !hasNuget)
        {
            return "Latest NuGet package.";
        }

        return $"Pinned NuGet version {spec.Nuget}.";
    }
}
