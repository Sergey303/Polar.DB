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
        AppendLatestStabilityTable(sb, engines);
        AppendLatestStorageTable(sb, engines);

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(ChartRenderer.RenderGroupedBarChart(
            "Phase Breakdown (latest series, tm ms)",
            new[] { "Load", "Build", "Reopen", "Lookup" },
            engines
                .Select(item => new ChartSeries(
                    item.EngineKey,
                    ResolveColor(colors, item.EngineKey),
                    new double?[]
                    {
                        GetMetricTm(item, "LoadMs"),
                        GetMetricTm(item, "BuildMs"),
                        GetMetricTm(item, "ReopenMs"),
                        GetMetricTm(item, "LookupMs")
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
            sb.AppendLine("<th>Elapsed tm: <code>" + NumberFormatter.HtmlEncode(engine) + "</code></th>");
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
                sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(GetMetricTm(series, "ElapsedMs")) + "</td>");
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
        sb.AppendLine("    <thead><tr><th>Experiment</th><th>Set</th><th>Timestamp</th><th>Dataset</th><th>Fairness</th><th>Elapsed tm per target</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var snapshot in others)
        {
            var summary = string.Join("; ", snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.EngineKey}: {NumberFormatter.StripHtml(NumberFormatter.FormatMilliseconds(GetMetricTm(item, "ElapsedMs")))}"));
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
            sb.AppendLine("    <thead><tr><th>Target</th><th>Set</th><th>Elapsed tm</th><th>Total bytes p50</th><th>Measured</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in model.LocalAnalyzedSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(item.EngineKey) + "</code></td>");
                sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(item.ComparisonSetId ?? "legacy/latest") + "</code></td>");
                sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(GetMetricTm(item, "ElapsedMs")) + "</td>");
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

        var elapsedTmMin = MinOrNull(engines.Select(item => GetMetricTm(item, "ElapsedMs")));
        var elapsedP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "ElapsedMs")));
        var loadTmMin = MinOrNull(engines.Select(item => GetMetricTm(item, "LoadMs")));
        var loadP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "LoadMs")));
        var buildTmMin = MinOrNull(engines.Select(item => GetMetricTm(item, "BuildMs")));
        var buildP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "BuildMs")));
        var reopenTmMin = MinOrNull(engines.Select(item => GetMetricTm(item, "ReopenMs")));
        var reopenP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "ReopenMs")));
        var lookupTmMin = MinOrNull(engines.Select(item => GetMetricTm(item, "LookupMs")));
        var lookupP95Min = MinOrNull(engines.Select(item => GetMetricP95(item, "LookupMs")));

        sb.AppendLine("  <h3>Timing</h3>");
        sb.AppendLine("  <p class=\"muted small\">tm is trimmed mean without outliers. p50 is median and remains a secondary stability/statistical value when shown. Lower is better.</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th>Target</th><th>Elapsed tm</th>" + (showElapsedP95 ? "<th>Elapsed p95</th>" : string.Empty) + "<th>Load tm</th>" + (showLoadP95 ? "<th>Load p95</th>" : string.Empty) + "<th>Build tm</th>" + (showBuildP95 ? "<th>Build p95</th>" : string.Empty) + "<th>Reopen tm</th>" + (showReopenP95 ? "<th>Reopen p95</th>" : string.Empty) + "<th>Lookup tm</th>" + (showLookupP95 ? "<th>Lookup p95</th>" : string.Empty));
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in engines)
        {
            var engineKey = GetStringMember(engine, "EngineKey") ?? "unknown";

            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(engineKey) + "</code></td>");
            sb.AppendLine(FormatMetricCell(GetMetricTm(engine, "ElapsedMs"), elapsedTmMin, MetricKind.Milliseconds));
            if (showElapsedP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "ElapsedMs"), elapsedP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricTm(engine, "LoadMs"), loadTmMin, MetricKind.Milliseconds));
            if (showLoadP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "LoadMs"), loadP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricTm(engine, "BuildMs"), buildTmMin, MetricKind.Milliseconds));
            if (showBuildP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "BuildMs"), buildP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricTm(engine, "ReopenMs"), reopenTmMin, MetricKind.Milliseconds));
            if (showReopenP95) sb.AppendLine(FormatMetricCell(GetMetricP95(engine, "ReopenMs"), reopenP95Min, MetricKind.Milliseconds));
            sb.AppendLine(FormatMetricCell(GetMetricTm(engine, "LookupMs"), lookupTmMin, MetricKind.Milliseconds));
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

    private static void AppendLatestStabilityTable<T>(System.Text.StringBuilder sb, IReadOnlyList<T> engines)
    {
        var hasStability = engines.Any(item =>
            GetMetricP95(item, "ElapsedMs").HasValue || GetMetricP99(item, "ElapsedMs").HasValue ||
            GetMetricMad(item, "ElapsedMs").HasValue || GetMetricJitterRatio(item, "ElapsedMs").HasValue);

        if (!hasStability)
        {
            return;
        }

        sb.AppendLine("  <h3>Stability</h3>");
        sb.AppendLine("  <p class=\"muted small\">p95/p99/trimmedMean10/MAD/jitter%/outliers. Empty cells mean the statistic could not be computed.</p>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th><th>Elapsed stability</th><th>Load stability</th><th>Build stability</th><th>Reopen stability</th><th>Lookup stability</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in engines)
        {
            var engineKey = GetStringMember(engine, "EngineKey") ?? "unknown";
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(engineKey) + "</code></td>");
            sb.AppendLine("        <td>" + FormatStabilityCell(engine, "ElapsedMs") + "</td>");
            sb.AppendLine("        <td>" + FormatStabilityCell(engine, "LoadMs") + "</td>");
            sb.AppendLine("        <td>" + FormatStabilityCell(engine, "BuildMs") + "</td>");
            sb.AppendLine("        <td>" + FormatStabilityCell(engine, "ReopenMs") + "</td>");
            sb.AppendLine("        <td>" + FormatStabilityCell(engine, "LookupMs") + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static string FormatStabilityCell<T>(T engine, string metricName)
    {
        var p95 = GetMetricP95(engine, metricName);
        var p99 = GetMetricP99(engine, metricName);
        var trimmedMean = GetMetricTrimmedMean10(engine, metricName);
        var mad = GetMetricMad(engine, metricName);
        var jitter = GetMetricJitterRatio(engine, metricName);
        var outlierCount = GetMetricOutlierCount(engine, metricName);

        var parts = new List<string>();
        if (p95.HasValue) parts.Add("p95:" + NumberFormatter.FormatMilliseconds(p95));
        if (p99.HasValue) parts.Add("p99:" + NumberFormatter.FormatMilliseconds(p99));
        if (trimmedMean.HasValue) parts.Add("tm:" + NumberFormatter.FormatMilliseconds(trimmedMean));
        if (mad.HasValue) parts.Add("mad:" + NumberFormatter.FormatMilliseconds(mad));
        if (jitter.HasValue) parts.Add("jit:" + (jitter.Value * 100).ToString("0.#", Invariant) + "%");
        if (outlierCount.HasValue) parts.Add("out:" + outlierCount.Value);

        return parts.Count > 0
            ? "<span class=\"mono small\">" + string.Join(" ", parts) + "</span>"
            : "<span class=\"muted\">n/a</span>";
    }

    private static double? GetMetricP99(object? source, string metricName)
    {
        var stats = GetMemberValue(source, metricName);
        return GetOptionalMetricValue(stats, "P99", "P99Value", "Percentile99", "NinetyNinthPercentile");
    }

    private static double? GetMetricTrimmedMean10(object? source, string metricName)
    {
        var stats = GetMemberValue(source, metricName);
        return GetOptionalMetricValue(stats, "TrimmedMean10", "TrimmedMean", "TrimmedMean10Value");
    }

    private static double? GetMetricMad(object? source, string metricName)
    {
        var stats = GetMemberValue(source, metricName);
        return GetOptionalMetricValue(stats, "Mad", "MAD", "MadValue", "MedianAbsoluteDeviation");
    }

    private static double? GetMetricJitterRatio(object? source, string metricName)
    {
        var stats = GetMemberValue(source, metricName);
        return GetOptionalMetricValue(stats, "JitterRatio", "Jitter", "JitterRatioValue");
    }

    private static int? GetMetricOutlierCount(object? source, string metricName)
    {
        var stats = GetMemberValue(source, metricName);
        if (stats is null)
        {
            return null;
        }

        var statsType = stats.GetType();
        var property = statsType.GetProperty("OutlierCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is not null)
        {
            var value = property.GetValue(stats);
            if (value is int i) return i;
            if (value is IConvertible conv) return Convert.ToInt32(conv, Invariant);
        }

        var field = statsType.GetField("OutlierCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (field is not null)
        {
            var value = field.GetValue(stats);
            if (value is int i) return i;
            if (value is IConvertible conv) return Convert.ToInt32(conv, Invariant);
        }

        return null;
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

    /// <summary>
    /// Gets a metric value from the Metrics dictionary if available,
    /// otherwise falls back to reflection-based property access.
    /// </summary>
    private static MetricSeriesStats? GetMetricStats(object? source, string metricName)
    {
        if (source is null)
        {
            return null;
        }

        // Try Metrics dictionary first (generic metrics)
        var metricsDict = GetMemberValue(source, "Metrics") as IReadOnlyDictionary<string, MetricSeriesStats>;
        if (metricsDict is not null && metricsDict.TryGetValue(metricName, out var stats))
        {
            return stats;
        }

        // Fall back to reflection-based property access (fixed properties)
        return GetMemberValue(source, metricName) as MetricSeriesStats;
    }

    /// <summary>
    /// Gets the primary metric value: trimmed mean (tm) if available, falls back to p50/median.
    /// This is the main metric used for timing comparisons throughout the report.
    /// </summary>
    private static double? GetMetricTm(object? source, string metricName)
    {
        var stats = GetMetricStats(source, metricName);
        // Try trimmed mean first (primary metric)
        var tm = GetOptionalMetricValue(
            stats,
            "TrimmedMean10",
            "TrimmedMean",
            "TrimmedMean10Ms",
            "TrimmedMeanMs",
            "Tm",
            "TM",
            "TmMs",
            "MeanWithoutOutliers",
            "MeanNoOutliers");
        if (tm.HasValue)
        {
            return tm;
        }
        // Fallback to p50/median for old artifacts that don't have trimmed mean
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

    private static double? GetMetricP50(object? source, string metricName)
    {
        var stats = GetMetricStats(source, metricName);
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
        var stats = GetMetricStats(source, metricName);
        return GetP95(stats);
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

    // ========================================================================
    // Thematic Metric Sections (using MetricReportCatalog)
    // ========================================================================

    /// <summary>
    /// Renders all thematic metric sections from the MetricReportCatalog.
    /// Skips sections that have no matching metrics in the data.
    /// </summary>
    public static void AppendThematicMetricSections(System.Text.StringBuilder sb, ExperimentIndexModel model)
    {
        // Collect all available metric keys from latest engines and local analyzed series
        var availableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // From latest engines snapshot
        if (model.LatestEngines?.Snapshot is not null)
        {
            foreach (var engine in model.LatestEngines.Snapshot.EngineSeries)
            {
                AddMetricKeys(engine, availableKeys);
            }
        }

        // From local analyzed series
        foreach (var series in model.LocalAnalyzedSeries)
        {
            AddMetricKeys(series, availableKeys);
        }

        if (availableKeys.Count == 0)
        {
            return;
        }

        // Render each known section
        foreach (var sectionName in MetricReportCatalog.SectionOrder)
        {
            if (sectionName == "All Metrics Appendix")
            {
                continue; // handled separately
            }

            var descriptors = MetricReportCatalog.GetDescriptorsForSection(sectionName);
            if (descriptors.Count == 0)
            {
                continue;
            }

            // Check if any of the section's metrics are available
            var matchingDescriptors = descriptors
                .Where(d => availableKeys.Contains(d.Key))
                .ToArray();

            if (matchingDescriptors.Length == 0)
            {
                continue;
            }

            AppendMetricTableSection(sb, sectionName, matchingDescriptors, model);
        }

        // Render All Metrics Appendix with unknown/uncategorized metrics
        AppendAllMetricsAppendix(sb, model, availableKeys);
    }

    /// <summary>
    /// Collects all metric keys from an engine entry (both fixed properties and Metrics dictionary).
    /// </summary>
    private static void AddMetricKeys(object? source, HashSet<string> keys)
    {
        if (source is null)
        {
            return;
        }

        // Add keys from Metrics dictionary
        var metricsDict = GetMemberValue(source, "Metrics") as IReadOnlyDictionary<string, MetricSeriesStats>;
        if (metricsDict is not null)
        {
            foreach (var key in metricsDict.Keys)
            {
                keys.Add(key);
            }
        }

        // Add fixed property names that have MetricSeriesStats values
        var sourceType = source.GetType();
        foreach (var prop in sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.PropertyType == typeof(MetricSeriesStats))
            {
                keys.Add(prop.Name);
            }
        }
    }

    /// <summary>
    /// Renders one thematic metric table section.
    /// </summary>
    private static void AppendMetricTableSection(
        System.Text.StringBuilder sb,
        string sectionName,
        IReadOnlyList<MetricDescriptor> descriptors,
        ExperimentIndexModel model)
    {
        sb.AppendLine($"<section class=\"card wide\">");
        sb.AppendLine($"  <h2>{NumberFormatter.HtmlEncode(sectionName)}</h2>");

        // Collect engines from latest engines snapshot
        var engines = model.LatestEngines?.Snapshot?.EngineSeries
            .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (engines is null || engines.Length == 0)
        {
            sb.AppendLine("  <p class=\"muted\">No engine comparison data available for this section.</p>");
            sb.AppendLine("</section>");
            return;
        }

        // Build table header
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th>Metric</th>");
        foreach (var engine in engines)
        {
            var engineKey = GetStringMember(engine, "EngineKey") ?? "unknown";
            sb.AppendLine($"        <th><code>{NumberFormatter.HtmlEncode(engineKey)}</code></th>");
        }

        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");

        foreach (var descriptor in descriptors)
        {
            var statName = descriptor.PreferredStat switch
            {
                PreferredStat.P50 => "p50",
                PreferredStat.P95 => "p95",
                PreferredStat.Average => "avg",
                PreferredStat.Max => "max",
                PreferredStat.Min => "min",
                _ => "p50"
            };

            sb.AppendLine("      <tr>");
            sb.AppendLine($"        <td><span title=\"{NumberFormatter.HtmlEncode(descriptor.Description ?? descriptor.Key)}\">{NumberFormatter.HtmlEncode(descriptor.Label)}</span><br/><span class=\"muted small\">{statName}</span></td>");

            // Compute best value based on direction
            var values = engines
                .Select(e => GetPreferredStatValue(e, descriptor))
                .ToArray();

            var bestValue = FindBestValue(values, descriptor.Direction);

            foreach (var value in values)
            {
                sb.AppendLine(FormatCatalogMetricCell(value, bestValue, descriptor));
            }

            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Gets the preferred statistic value for a metric from an engine entry.
    /// </summary>
    private static double? GetPreferredStatValue(object? engine, MetricDescriptor descriptor)
    {
        var stats = GetMetricStats(engine, descriptor.Key);
        if (stats is null)
        {
            return null;
        }

        return descriptor.PreferredStat switch
        {
            PreferredStat.P50 => stats.P50 ?? stats.Median,
            PreferredStat.P95 => stats.P95,
            PreferredStat.Average => stats.Average,
            PreferredStat.Max => stats.Max,
            PreferredStat.Min => stats.Min,
            _ => stats.P50 ?? stats.Median
        };
    }

    /// <summary>
    /// Finds the best value among an array of values based on direction.
    /// </summary>
    private static double? FindBestValue(double?[] values, MetricDirection direction)
    {
        var valid = values
            .Where(v => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value))
            .Select(v => v!.Value)
            .ToArray();

        if (valid.Length == 0)
        {
            return null;
        }

        return direction switch
        {
            MetricDirection.LowerIsBetter => valid.Min(),
            MetricDirection.HigherIsBetter => valid.Max(),
            MetricDirection.ZeroIsBest => 0.0,
            MetricDirection.OneIsBest => 1.0,
            MetricDirection.Neutral => null,
            _ => valid.Min()
        };
    }

    /// <summary>
    /// Formats a metric cell using the catalog descriptor for direction-aware highlighting.
    /// </summary>
    private static string FormatCatalogMetricCell(double? value, double? bestValue, MetricDescriptor descriptor)
    {
        var formatted = FormatMetricValue(value, descriptor.Unit);
        if (!value.HasValue)
        {
            return "        <td class=\"metric-cell\"><div class=\"metric-main\">" + formatted + "</div></td>";
        }

        var isBest = IsBestValue(value.Value, bestValue, descriptor.Direction);
        var css = "metric-cell";
        if (isBest)
        {
            css += " metric-best";
        }
        else if (descriptor.Direction == MetricDirection.ZeroIsBest && value.Value != 0.0)
        {
            css += " metric-warn";
        }
        else if (descriptor.Direction == MetricDirection.OneIsBest && !AreSame(value.Value, 1.0))
        {
            css += " metric-warn";
        }

        var ratio = FormatCatalogRatio(value.Value, bestValue, descriptor);
        return "        <td class=\"" + css + "\"><div class=\"metric-main\">" + formatted + "</div><div class=\"metric-ratio\">" + NumberFormatter.HtmlEncode(ratio) + "</div></td>";
    }

    /// <summary>
    /// Determines if a value is the best according to the metric direction.
    /// </summary>
    private static bool IsBestValue(double value, double? bestValue, MetricDirection direction)
    {
        if (!bestValue.HasValue)
        {
            return false;
        }

        return direction switch
        {
            MetricDirection.LowerIsBetter => AreSame(value, bestValue.Value),
            MetricDirection.HigherIsBetter => AreSame(value, bestValue.Value),
            MetricDirection.ZeroIsBest => AreSame(value, 0.0),
            MetricDirection.OneIsBest => AreSame(value, 1.0),
            MetricDirection.Neutral => false,
            _ => false
        };
    }

    /// <summary>
    /// Formats a ratio string for catalog metric cells.
    /// For LowerIsBetter: ratio = value / bestValue (shows how many times worse than best).
    /// For HigherIsBetter: ratio = bestValue / value (shows how many times better the best is).
    /// </summary>
    private static string FormatCatalogRatio(double value, double? bestValue, MetricDescriptor descriptor)
    {
        if (!bestValue.HasValue || descriptor.Direction == MetricDirection.Neutral)
        {
            return string.Empty;
        }

        if (descriptor.Direction == MetricDirection.ZeroIsBest)
        {
            return AreSame(value, 0.0) ? "ideal" : "non-zero";
        }

        if (descriptor.Direction == MetricDirection.OneIsBest)
        {
            return AreSame(value, 1.0) ? "ideal" : "off";
        }

        if (AreSame(value, bestValue.Value))
        {
            return "best";
        }

        if (Math.Abs(bestValue.Value) < 1e-12)
        {
            return "+" + FormatPlainCatalogMetric(value - bestValue.Value, descriptor.Unit) + " over best";
        }

        // For LowerIsBetter: value / bestValue shows how many times worse than the best (e.g. ×2.5 best)
        // For HigherIsBetter: bestValue / value shows how many times better the best is (e.g. ×2.5 best)
        var ratio = descriptor.Direction == MetricDirection.LowerIsBetter
            ? value / bestValue.Value
            : bestValue.Value / value;

        return "×" + ratio.ToString("0.##", Invariant) + " best";
    }


    /// <summary>
    /// Formats a plain metric value for ratio display.
    /// </summary>
    private static string FormatPlainCatalogMetric(double value, MetricUnit unit)
    {
        return unit switch
        {
            MetricUnit.Milliseconds or MetricUnit.MillisecondsPerQuery or MetricUnit.MillisecondsPerRow => FormatPlainMetric(value, MetricKind.Milliseconds),
            MetricUnit.Bytes => NumberFormatter.FormatBinaryBytes(value),
            _ => value.ToString("0.###", Invariant)
        };
    }

    /// <summary>
    /// Formats a metric value according to its unit.
    /// </summary>
    private static string FormatMetricValue(double? value, MetricUnit unit)
    {
        if (!value.HasValue)
        {
            return "<span class=\"muted\">n/a</span>";
        }

        return unit switch
        {
            MetricUnit.Milliseconds => NumberFormatter.FormatMilliseconds(value),
            MetricUnit.Bytes => NumberFormatter.FormatBytes(value),
            MetricUnit.Percent => $"<span title=\"raw: {value.Value.ToString("0.###############", Invariant)}\">{value.Value.ToString("0.##", Invariant)}%</span>",
            MetricUnit.Ratio => $"<span title=\"raw: {value.Value.ToString("0.###############", Invariant)}\">{value.Value.ToString("0.###", Invariant)}</span>",
            MetricUnit.PerSecond or MetricUnit.RowsPerSecond or MetricUnit.QueriesPerSecond => $"<span title=\"raw: {value.Value.ToString("0.###############", Invariant)}\">{NumberFormatter.FormatScientificWithUnit(value.Value, string.Empty, false)}/s</span>",
            MetricUnit.MillisecondsPerQuery => NumberFormatter.FormatMilliseconds(value),
            MetricUnit.MillisecondsPerRow => NumberFormatter.FormatMilliseconds(value),
            MetricUnit.Count => $"<span title=\"raw: {value.Value.ToString("0.###############", Invariant)}\">{value.Value.ToString("0.###", Invariant)}</span>",
            MetricUnit.None => $"<span title=\"raw: {value.Value.ToString("0.###############", Invariant)}\">{value.Value.ToString("0.###", Invariant)}</span>",
            _ => $"<span title=\"raw: {value.Value.ToString("0.###############", Invariant)}\">{value.Value.ToString("0.###", Invariant)}</span>"
        };
    }

    /// <summary>
    /// Renders the "All Metrics Appendix" with ALL available metrics (both known and unknown).
    /// This section is collapsed by default and provides a complete reference of every metric
    /// key and its p50 value across all engines.
    /// </summary>
    private static void AppendAllMetricsAppendix(
        System.Text.StringBuilder sb,
        ExperimentIndexModel model,
        HashSet<string> availableKeys)
    {
        if (availableKeys.Count == 0)
        {
            return;
        }

        // Collect ALL keys sorted: known catalog keys first (by section order, then label),
        // then unknown keys alphabetically
        var knownKeys = new List<string>();
        var unknownKeys = new List<string>();
        foreach (var key in availableKeys)
        {
            if (MetricReportCatalog.TryGetDescriptor(key) is not null)
            {
                knownKeys.Add(key);
            }
            else
            {
                unknownKeys.Add(key);
            }
        }

        // Sort known keys by section order then priority/label
        var knownKeyOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sectionIndex = 0;
        foreach (var section in MetricReportCatalog.SectionOrder)
        {
            if (section == "All Metrics Appendix") continue;
            var descriptors = MetricReportCatalog.GetDescriptorsForSection(section);
            foreach (var d in descriptors.OrderBy(d => d.Priority).ThenBy(d => d.Label))
            {
                if (!knownKeyOrder.ContainsKey(d.Key))
                {
                    knownKeyOrder[d.Key] = sectionIndex++;
                }
            }
        }
        // Any known key not in section order gets appended at the end of known keys
        knownKeys.Sort((a, b) =>
        {
            var ai = knownKeyOrder.TryGetValue(a, out var ia) ? ia : int.MaxValue;
            var bi = knownKeyOrder.TryGetValue(b, out var ib) ? ib : int.MaxValue;
            var cmp = ai.CompareTo(bi);
            return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });
        unknownKeys.Sort(StringComparer.OrdinalIgnoreCase);

        var allKeys = knownKeys.Concat(unknownKeys).ToArray();

        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <details>");
        sb.AppendLine("    <summary><h2 style=\"display:inline\">All Metrics Appendix</h2></summary>");
        sb.AppendLine("    <p class=\"muted small\">Complete listing of every available metric key and its p50 value across all engines. Known catalog metrics appear first (grouped by thematic section), followed by unknown/uncategorized metrics.</p>");

        var engines = model.LatestEngines?.Snapshot?.EngineSeries
            .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (engines is null || engines.Length == 0)
        {
            sb.AppendLine("    <p class=\"muted\">No engine comparison data available.</p>");
        }
        else
        {
            sb.AppendLine("    <table>");
            sb.AppendLine("      <thead>");
            sb.AppendLine("        <tr>");
            sb.AppendLine("          <th>Metric Key</th>");
            sb.AppendLine("          <th>Section</th>");
            foreach (var engine in engines)
            {
                var engineKey = GetStringMember(engine, "EngineKey") ?? "unknown";
                sb.AppendLine($"          <th><code>{NumberFormatter.HtmlEncode(engineKey)}</code></th>");
            }

            sb.AppendLine("        </tr>");
            sb.AppendLine("      </thead>");
            sb.AppendLine("      <tbody>");

            string? lastSection = null;
            foreach (var key in allKeys)
            {
                var descriptor = MetricReportCatalog.TryGetDescriptor(key);
                var section = descriptor?.Section ?? "Uncategorized";

                // Insert a section header row when section changes
                if (section != lastSection)
                {
                    if (lastSection is not null)
                    {
                        // Close previous group visually with a subtle separator
                    }
                    sb.AppendLine("        <tr class=\"appendix-section\">");
                    sb.AppendLine($"          <td colspan=\"{2 + engines.Length}\"><strong>{NumberFormatter.HtmlEncode(section)}</strong></td>");
                    sb.AppendLine("        </tr>");
                    lastSection = section;
                }

                sb.AppendLine("        <tr>");
                sb.AppendLine($"          <td><code>{NumberFormatter.HtmlEncode(key)}</code></td>");
                sb.AppendLine($"          <td class=\"muted small\">{NumberFormatter.HtmlEncode(section)}</td>");
                foreach (var engine in engines)
                {
                    var stats = GetMetricStats(engine, key);
                    var p50 = stats?.P50 ?? stats?.Median;
                    // Use safe unit-aware formatting based on catalog descriptor or fallback
                    var formatted = descriptor is not null
                        ? FormatMetricValue(p50, descriptor.Unit)
                        : FormatSafeMetricValue(p50);
                    sb.AppendLine($"          <td>{formatted}</td>");
                }

                sb.AppendLine("        </tr>");
            }

            sb.AppendLine("      </tbody>");
            sb.AppendLine("    </table>");
        }

        sb.AppendLine("  </details>");
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Safely formats a metric value when the unit is unknown.
    /// Uses heuristics: large values are formatted as bytes or milliseconds,
    /// small values as plain numbers.
    /// </summary>
    private static string FormatSafeMetricValue(double? value)
    {
        if (!value.HasValue)
        {
            return "<span class=\"muted\">n/a</span>";
        }

        var raw = value.Value;
        var abs = Math.Abs(raw);

        // Heuristic: values >= 1_000_000 are likely bytes
        if (abs >= 1_000_000)
        {
            return NumberFormatter.FormatBytes(value);
        }

        // Heuristic: values >= 1.0 and < 1_000_000 could be milliseconds or counts
        if (abs >= 1.0)
        {
            return $"<span title=\"raw: {raw.ToString("0.###############", Invariant)}\">{raw.ToString("0.###", Invariant)}</span>";
        }

        // Small values: could be ratios or milliseconds
        return $"<span title=\"raw: {raw.ToString("0.###############", Invariant)}\">{raw.ToString("0.###############", Invariant)}</span>";
    }
}
