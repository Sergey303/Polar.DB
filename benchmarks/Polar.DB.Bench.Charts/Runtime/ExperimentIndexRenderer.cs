using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Builds the main human-readable experiment page.
/// The page is fully static HTML with inline SVG charts and links to machine-readable artifacts.
/// </summary>
internal sealed class ExperimentIndexRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public string BuildHtml(ExperimentIndexModel model)
    {
        var historyEnabled = ResolveEnabledFlag(model.Manifest.Compare.History);
        var otherEnabled = ResolveEnabledFlag(model.Manifest.Compare.OtherExperiments);

        var sb = new StringBuilder(capacity: 64 * 1024);
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine($"  <title>{Encode(model.Manifest.Title)} - Benchmark Experiment</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    :root {");
        sb.AppendLine("      --bg: #f7f8f9;");
        sb.AppendLine("      --panel: #ffffff;");
        sb.AppendLine("      --ink: #111315;");
        sb.AppendLine("      --muted: #5b636e;");
        sb.AppendLine("      --line: #d9dde3;");
        sb.AppendLine("      --accent: #0f6f9f;");
        sb.AppendLine("      --accent-2: #a04f15;");
        sb.AppendLine("      --good: #1f7a3f;");
        sb.AppendLine("      --warn: #9a6a05;");
        sb.AppendLine("      --bad: #9f1f2a;");
        sb.AppendLine("      --mono: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;");
        sb.AppendLine("      --ui: \"Segoe UI\", \"Noto Sans\", Arial, sans-serif;");
        sb.AppendLine("    }");
        sb.AppendLine("    * { box-sizing: border-box; }");
        sb.AppendLine("    body { margin: 0; background: linear-gradient(170deg, #eef2f5 0%, var(--bg) 50%, #f4f7fb 100%); color: var(--ink); font-family: var(--ui); }");
        sb.AppendLine("    .wrap { max-width: 1240px; margin: 0 auto; padding: 20px 16px 32px; }");
        sb.AppendLine("    .header { background: var(--panel); border: 1px solid var(--line); border-radius: 14px; padding: 16px 18px; box-shadow: 0 5px 20px rgba(0, 0, 0, 0.03); }");
        sb.AppendLine("    h1 { margin: 0 0 8px; font-size: clamp(1.35rem, 2vw, 2.05rem); line-height: 1.2; }");
        sb.AppendLine("    h2 { margin: 0 0 10px; font-size: 1.15rem; }");
        sb.AppendLine("    p { margin: 8px 0; line-height: 1.5; }");
        sb.AppendLine("    .meta { color: var(--muted); font-size: 0.92rem; }");
        sb.AppendLine("    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 12px; margin-top: 12px; }");
        sb.AppendLine("    .card { background: var(--panel); border: 1px solid var(--line); border-radius: 14px; padding: 14px 14px 12px; box-shadow: 0 4px 16px rgba(0, 0, 0, 0.025); }");
        sb.AppendLine("    .card.wide { margin-top: 12px; }");
        sb.AppendLine("    .muted { color: var(--muted); }");
        sb.AppendLine("    .mono { font-family: var(--mono); }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 0.92rem; margin-top: 8px; }");
        sb.AppendLine("    th, td { border-bottom: 1px solid var(--line); padding: 7px 8px; vertical-align: top; text-align: left; }");
        sb.AppendLine("    th { font-weight: 650; background: #f3f6f9; }");
        sb.AppendLine("    tr:last-child td { border-bottom: none; }");
        sb.AppendLine("    code { font-family: var(--mono); font-size: 0.87em; }");
        sb.AppendLine("    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; margin-top: 8px; }");
        sb.AppendLine("    .kpi { border: 1px solid var(--line); border-radius: 10px; background: #fafbfd; padding: 8px; }");
        sb.AppendLine("    .kpi .label { display: block; color: var(--muted); font-size: 0.82rem; margin-bottom: 2px; }");
        sb.AppendLine("    .kpi .val { font-size: 0.93rem; font-weight: 650; }");
        sb.AppendLine("    .legend { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 8px; }");
        sb.AppendLine("    .legend-item { display: inline-flex; align-items: center; gap: 6px; font-size: 0.86rem; color: var(--muted); }");
        sb.AppendLine("    .swatch { width: 11px; height: 11px; border-radius: 2px; border: 1px solid rgba(0, 0, 0, 0.15); }");
        sb.AppendLine("    .status-on { color: var(--good); font-weight: 650; }");
        sb.AppendLine("    .status-off { color: var(--warn); font-weight: 650; }");
        sb.AppendLine("    .chart-wrap { overflow-x: auto; margin-top: 8px; border: 1px solid var(--line); border-radius: 10px; background: #fff; }");
        sb.AppendLine("    svg.chart { min-width: 720px; width: 100%; height: auto; display: block; }");
        sb.AppendLine("    ul.clean { margin: 8px 0 0; padding-left: 18px; }");
        sb.AppendLine("    ul.clean li { margin: 4px 0; }");
        sb.AppendLine("    a { color: var(--accent); text-decoration: none; }");
        sb.AppendLine("    a:hover { text-decoration: underline; }");
        sb.AppendLine("    .small { font-size: 0.84rem; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"wrap\">");

        AppendHeader(sb, model);
        AppendIdentitySection(sb, model);
        AppendLatestEnginesSection(sb, model);
        AppendHistorySection(sb, model, historyEnabled);
        AppendOtherExperimentsSection(sb, model, otherEnabled);
        AppendArtifactsSection(sb, model);

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"header\">");
        sb.AppendLine($"  <h1>{Encode(model.Manifest.Title)}</h1>");
        sb.AppendLine($"  <p class=\"muted\">Experiment: <code>{Encode(model.Manifest.ExperimentKey)}</code></p>");
        if (!string.IsNullOrWhiteSpace(model.Manifest.Description))
        {
            sb.AppendLine($"  <p>{Encode(model.Manifest.Description!)}</p>");
        }

        sb.AppendLine("  <p class=\"meta\">");
        sb.AppendLine($"    Generated: <span class=\"mono\">{Encode(model.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", Invariant))}</span>.");
        sb.AppendLine("    Numbers use scientific display for large values; exact raw values are available in tooltip.");
        sb.AppendLine("  </p>");
        sb.AppendLine("</section>");
    }

    private static void AppendIdentitySection(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"grid\">");
        sb.AppendLine("  <article class=\"card\">");
        sb.AppendLine("    <h2>Dataset</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <tr><th>Profile</th><td><code>" + Encode(model.Manifest.Dataset.ProfileKey) + "</code></td></tr>");
        sb.AppendLine("      <tr><th>Count</th><td>" + FormatGeneralNumber(model.Manifest.Dataset.RecordCount) + "</td></tr>");
        sb.AppendLine("      <tr><th>Seed</th><td>" + (model.Manifest.Dataset.Seed?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Notes</th><td>" + Encode(model.Manifest.Dataset.Notes ?? string.Empty) + "</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </article>");

        sb.AppendLine("  <article class=\"card\">");
        sb.AppendLine("    <h2>Workload</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <tr><th>Type</th><td><code>" + Encode(model.Manifest.Workload.WorkloadKey) + "</code></td></tr>");
        sb.AppendLine("      <tr><th>Lookup</th><td>" + (model.Manifest.Workload.LookupCount?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Batches</th><td>" + (model.Manifest.Workload.BatchCount?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Batch size</th><td>" + (model.Manifest.Workload.BatchSize?.ToString(Invariant) ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Notes</th><td>" + Encode(model.Manifest.Workload.Notes ?? string.Empty) + "</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </article>");

        sb.AppendLine("  <article class=\"card\">");
        sb.AppendLine("    <h2>Fairness</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <tr><th>Profile</th><td><code>" + Encode(model.Manifest.FairnessProfile.FairnessProfileKey) + "</code></td></tr>");
        sb.AppendLine("      <tr><th>Notes</th><td>" + Encode(model.Manifest.FairnessProfile.Notes ?? string.Empty) + "</td></tr>");
        sb.AppendLine("      <tr><th>Research</th><td>" + Encode(model.Manifest.ResearchQuestionId ?? "n/a") + "</td></tr>");
        sb.AppendLine("      <tr><th>Hypothesis</th><td>" + Encode(model.Manifest.HypothesisId ?? "n/a") + "</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </article>");
        sb.AppendLine("</section>");

        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Engines</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Engine</th><th>Spec</th><th>Runtime semantics</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var (engineKey, engineSpec) in model.Manifest.Engines.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var spec = string.IsNullOrWhiteSpace(engineSpec.Nuget)
                ? "{}"
                : "{ \"nuget\": \"" + Encode(engineSpec.Nuget!) + "\" }";
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + Encode(engineKey) + "</code></td>");
            sb.AppendLine("        <td><code>" + spec + "</code></td>");
            sb.AppendLine("        <td>" + Encode(DescribeRuntimeSemantics(engineKey, engineSpec)) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</section>");
    }

    private static void AppendLatestEnginesSection(StringBuilder sb, ExperimentIndexModel model)
    {
        var latestEngines = model.LatestEngines;
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Latest Engine Comparison</h2>");

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
            sb.AppendLine("  <p class=\"muted\">No complete successful measured series is currently available for all configured engines.</p>");
            AppendNotes(sb, latestEngines.Notes);
            sb.AppendLine("</section>");
            return;
        }

        var snapshot = latestEngines.Snapshot;
        sb.AppendLine("  <div class=\"kpis\">");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Comparison set</span><span class=\"val mono\">" + Encode(snapshot.ComparisonSetId ?? "legacy/latest") + "</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Timestamp</span><span class=\"val mono\">" + Encode(snapshot.SnapshotTimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", Invariant)) + " UTC</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Dataset</span><span class=\"val mono\">" + Encode(snapshot.DatasetProfileKey ?? "mixed") + "</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Fairness</span><span class=\"val mono\">" + Encode(snapshot.FairnessProfileKey ?? "mixed") + "</span></div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th>Engine</th><th>Measured</th><th>Technical</th><th>Semantic</th>");
        sb.AppendLine("        <th>Elapsed median</th><th>Load median</th><th>Build median</th><th>Reopen median</th><th>Lookup median</th>");
        sb.AppendLine("        <th>Total bytes median</th><th>Primary bytes median</th><th>Side bytes median</th>");
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");
        foreach (var engine in snapshot.EngineSeries.OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + Encode(engine.EngineKey) + "</code></td>");
            sb.AppendLine("        <td>" + engine.MeasuredRunCount + "</td>");
            sb.AppendLine("        <td>" + engine.TechnicalSuccessCount + "/" + engine.MeasuredRunCount + "</td>");
            sb.AppendLine("        <td>" + engine.SemanticSuccessCount + "/" + engine.SemanticEvaluatedCount + "</td>");
            sb.AppendLine("        <td>" + FormatMilliseconds(engine.ElapsedMs.Median) + "</td>");
            sb.AppendLine("        <td>" + FormatMilliseconds(engine.LoadMs.Median) + "</td>");
            sb.AppendLine("        <td>" + FormatMilliseconds(engine.BuildMs.Median) + "</td>");
            sb.AppendLine("        <td>" + FormatMilliseconds(engine.ReopenMs.Median) + "</td>");
            sb.AppendLine("        <td>" + FormatMilliseconds(engine.LookupMs.Median) + "</td>");
            sb.AppendLine("        <td>" + FormatBytes(engine.TotalArtifactBytes.Median) + "</td>");
            sb.AppendLine("        <td>" + FormatBytes(engine.PrimaryArtifactBytes.Median) + "</td>");
            sb.AppendLine("        <td>" + FormatBytes(engine.SideArtifactBytes.Median) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(RenderGroupedBarChart(
            "Phase Breakdown (latest series, median ms)",
            new[] { "Load", "Build", "Reopen", "Lookup" },
            snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new ChartSeries(
                    item.EngineKey,
                    EngineColor(item.EngineKey),
                    new double?[]
                    {
                        item.LoadMs.Median,
                        item.BuildMs.Median,
                        item.ReopenMs.Median,
                        item.LookupMs.Median
                    }))
                .ToArray(),
            "ms",
            FormatMillisecondsAxis))
            ;
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(RenderGroupedBarChart(
            "Artifact Sizes (latest series, median bytes)",
            new[] { "Primary", "Side", "Total" },
            snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new ChartSeries(
                    item.EngineKey,
                    EngineColor(item.EngineKey),
                    new double?[]
                    {
                        item.PrimaryArtifactBytes.Median,
                        item.SideArtifactBytes.Median,
                        item.TotalArtifactBytes.Median
                    }))
                .ToArray(),
            "B",
            FormatBytesAxis))
            ;
        sb.AppendLine("  </div>");

        AppendNotes(sb, latestEngines.Notes);
        AppendExpectations(sb, latestEngines.DerivedExpectations);
        sb.AppendLine("</section>");
    }

    private static void AppendHistorySection(StringBuilder sb, ExperimentIndexModel model, bool historyEnabled)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>History</h2>");

        var latestHistory = model.LatestHistory;
        if (!historyEnabled)
        {
            sb.AppendLine("  <p class=\"status-off\">Disabled by experiment <code>compare.history</code> flag.</p>");
            sb.AppendLine("</section>");
            return;
        }

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
            sb.AppendLine("<th>Elapsed median: <code>" + Encode(engine) + "</code></th>");
        }

        sb.AppendLine("</tr></thead>");
        sb.AppendLine("    <tbody>");
        for (var i = 0; i < snapshots.Length; i++)
        {
            var snapshot = snapshots[i];
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + (i + 1) + "</td>");
            sb.AppendLine("        <td><code>" + Encode(snapshot.ComparisonSetId ?? "legacy") + "</code></td>");
            sb.AppendLine("        <td class=\"mono\">" + Encode(snapshot.SnapshotTimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", Invariant)) + "</td>");
            foreach (var engine in engines)
            {
                var series = snapshot.EngineSeries.FirstOrDefault(item =>
                    item.EngineKey.Equals(engine, StringComparison.OrdinalIgnoreCase));
                sb.AppendLine("        <td>" + FormatMilliseconds(series?.ElapsedMs.Median) + "</td>");
            }

            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(RenderHistoryChart(snapshots, engines));
        sb.AppendLine("  </div>");

        AppendNotes(sb, latestHistory.Notes);
        AppendExpectations(sb, latestHistory.DerivedExpectations);
        sb.AppendLine("</section>");
    }

    private static void AppendOtherExperimentsSection(StringBuilder sb, ExperimentIndexModel model, bool otherEnabled)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Context With Other Experiments</h2>");

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
            sb.AppendLine("  <p class=\"muted\">No resolved external snapshots were found for configured experiments.</p>");
            AppendNotes(sb, latestOther.Notes);
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Experiment</th><th>Set</th><th>Timestamp</th><th>Dataset</th><th>Fairness</th><th>Elapsed median per engine</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var snapshot in others)
        {
            var summary = string.Join("; ", snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.EngineKey}: {StripHtml(FormatMilliseconds(item.ElapsedMs.Median))}"));
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + Encode(snapshot.ExperimentKey) + "</code></td>");
            sb.AppendLine("        <td><code>" + Encode(snapshot.ComparisonSetId ?? "legacy") + "</code></td>");
            sb.AppendLine("        <td class=\"mono\">" + Encode(snapshot.SnapshotTimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", Invariant)) + "</td>");
            sb.AppendLine("        <td><code>" + Encode(snapshot.DatasetProfileKey ?? "mixed") + "</code></td>");
            sb.AppendLine("        <td><code>" + Encode(snapshot.FairnessProfileKey ?? "mixed") + "</code></td>");
            sb.AppendLine("        <td>" + Encode(summary) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        AppendNotes(sb, latestOther.Notes);
        AppendExpectations(sb, latestOther.DerivedExpectations);
        sb.AppendLine("</section>");
    }

    private static void AppendArtifactsSection(StringBuilder sb, ExperimentIndexModel model)
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
            sb.AppendLine("    <thead><tr><th>Engine</th><th>Set</th><th>Elapsed median</th><th>Total bytes median</th><th>Measured</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in model.LocalAnalyzedSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine("        <td><code>" + Encode(item.EngineKey) + "</code></td>");
                sb.AppendLine("        <td><code>" + Encode(item.ComparisonSetId ?? "legacy/latest") + "</code></td>");
                sb.AppendLine("        <td>" + FormatMilliseconds(item.ElapsedMs.Median) + "</td>");
                sb.AppendLine("        <td>" + FormatBytes(item.TotalArtifactBytes.Median) + "</td>");
                sb.AppendLine("        <td>" + item.MeasuredRunCount + "</td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        sb.AppendLine("</section>");
    }

    private static void AppendArtifactList(
        StringBuilder sb,
        string title,
        IReadOnlyList<ArtifactFileLink> files,
        int maxRows)
    {
        sb.AppendLine($"  <h3>{Encode(title)}</h3>");
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
                : Encode(file.LastWriteUtc.Value.ToString("yyyy-MM-dd HH:mm:ss", Invariant));
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><a href=\"./" + EncodeAttribute(file.RelativePath.Replace('\\', '/')) + "\"><code>" + Encode(fileName) + "</code></a></td>");
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

    private static void AppendNotes(StringBuilder sb, IReadOnlyList<string>? notes)
    {
        if (notes is null || notes.Count == 0)
        {
            return;
        }

        sb.AppendLine("  <h3>Notes</h3>");
        sb.AppendLine("  <ul class=\"clean\">");
        foreach (var note in notes.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            sb.AppendLine("    <li>" + Encode(note) + "</li>");
        }

        sb.AppendLine("  </ul>");
    }

    private static void AppendExpectations(StringBuilder sb, IReadOnlyList<string>? expectations)
    {
        if (expectations is null || expectations.Count == 0)
        {
            return;
        }

        sb.AppendLine("  <h3>Derived Expectations</h3>");
        sb.AppendLine("  <ul class=\"clean\">");
        foreach (var expectation in expectations.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            sb.AppendLine("    <li>" + Encode(expectation) + "</li>");
        }

        sb.AppendLine("  </ul>");
    }

    private static string RenderHistoryChart(IReadOnlyList<ComparisonSnapshot> snapshots, IReadOnlyList<string> engineKeys)
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
        var sb = new StringBuilder();
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
            sb.AppendLine($"  <text x=\"{(marginLeft - 8).ToString("0.###", Invariant)}\" y=\"{(y + 4).ToString("0.###", Invariant)}\" text-anchor=\"end\" font-size=\"11\" fill=\"#66717f\">{Encode(FormatMillisecondsAxis(value))}</text>");
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
                sb.AppendLine($"    <title>{Encode(tooltip)}</title>");
                sb.AppendLine("  </circle>");
            }
        }

        var labelStep = Math.Max(1, snapshots.Count / 6);
        for (var i = 0; i < snapshots.Count; i += labelStep)
        {
            var snapshot = snapshots[i];
            var x = marginLeft + (snapshots.Count > 1 ? i * xStep : plotWidth * 0.5);
            var label = SnapshotLabel(snapshot, maxChars: 14);
            sb.AppendLine($"  <text x=\"{x.ToString("0.###", Invariant)}\" y=\"{(marginTop + plotHeight + 18).ToString("0.###", Invariant)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#66717f\">{Encode(label)}</text>");
        }

        var legendX = marginLeft;
        var legendY = chartHeight - 24.0;
        foreach (var engine in engineKeys)
        {
            sb.AppendLine($"  <rect x=\"{legendX.ToString("0.###", Invariant)}\" y=\"{(legendY - 9).ToString("0.###", Invariant)}\" width=\"12\" height=\"12\" fill=\"{EngineColor(engine)}\" rx=\"2\" />");
            sb.AppendLine($"  <text x=\"{(legendX + 16).ToString("0.###", Invariant)}\" y=\"{legendY.ToString("0.###", Invariant)}\" font-size=\"11\" fill=\"#4e5a68\">{Encode(engine)}</text>");
            legendX += 130;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string RenderGroupedBarChart(
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
            return $"<p class=\"muted\">{Encode(title)}: no data.</p>";
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

        var sb = new StringBuilder();
        sb.AppendLine($"<svg class=\"chart\" viewBox=\"0 0 {chartWidth.ToString("0.###", Invariant)} {chartHeight.ToString("0.###", Invariant)}\" role=\"img\" aria-label=\"{EncodeAttribute(title)}\">");
        sb.AppendLine("  <rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine($"  <text x=\"18\" y=\"20\" font-size=\"14\" fill=\"#20252b\" font-weight=\"650\">{Encode(title)}</text>");

        const int ticks = 5;
        for (var i = 0; i <= ticks; i++)
        {
            var ratio = i / (double)ticks;
            var y = marginTop + plotHeight - ratio * plotHeight;
            var value = ratio * top;
            sb.AppendLine($"  <line x1=\"{marginLeft.ToString("0.###", Invariant)}\" y1=\"{y.ToString("0.###", Invariant)}\" x2=\"{(marginLeft + plotWidth).ToString("0.###", Invariant)}\" y2=\"{y.ToString("0.###", Invariant)}\" stroke=\"#e6eaf0\" stroke-width=\"1\" />");
            sb.AppendLine($"  <text x=\"{(marginLeft - 8).ToString("0.###", Invariant)}\" y=\"{(y + 4).ToString("0.###", Invariant)}\" text-anchor=\"end\" font-size=\"11\" fill=\"#66717f\">{Encode(axisFormatter(value))}</text>");
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
                sb.AppendLine($"    <title>{Encode(tooltip)}</title>");
                sb.AppendLine("  </rect>");
            }

            var labelX = marginLeft + categoryIndex * categoryWidth + categoryWidth * 0.5;
            sb.AppendLine($"  <text x=\"{labelX.ToString("0.###", Invariant)}\" y=\"{(marginTop + plotHeight + 18).ToString("0.###", Invariant)}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#66717f\">{Encode(categories[categoryIndex])}</text>");
        }

        var legendX = marginLeft;
        var legendY = chartHeight - 24.0;
        foreach (var item in series)
        {
            sb.AppendLine($"  <rect x=\"{legendX.ToString("0.###", Invariant)}\" y=\"{(legendY - 9).ToString("0.###", Invariant)}\" width=\"12\" height=\"12\" fill=\"{item.Color}\" rx=\"2\" />");
            sb.AppendLine($"  <text x=\"{(legendX + 16).ToString("0.###", Invariant)}\" y=\"{legendY.ToString("0.###", Invariant)}\" font-size=\"11\" fill=\"#4e5a68\">{Encode(item.Name)}</text>");
            legendX += 130;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string FormatGeneralNumber(long value)
    {
        var asDouble = (double)value;
        return "<span title=\"raw: " + Encode(value.ToString(Invariant)) + "\">" + FormatScientificWithUnit(asDouble, unit: string.Empty, includeSecondary: false) + "</span>";
    }

    private static string FormatMilliseconds(double? value)
    {
        if (!value.HasValue)
        {
            return "<span class=\"muted\">n/a</span>";
        }

        var raw = value.Value;
        var main = FormatScientificWithUnit(raw, "ms", includeSecondary: false);
        var abs = Math.Abs(raw);
        if (abs >= 1000.0)
        {
            var seconds = raw / 1000.0d;
            var secondsText = seconds.ToString("0.###", Invariant) + " s";
            return "<span title=\"raw: " + Encode(raw.ToString("0.###############", Invariant)) + " ms\">" + main + " (" + Encode(secondsText) + ")</span>";
        }

        if (abs > 0 && abs < 1.0)
        {
            var microsText = (raw * 1000.0d).ToString("0.###", Invariant) + " us";
            return "<span title=\"raw: " + Encode(raw.ToString("0.###############", Invariant)) + " ms\">" + main + " (" + Encode(microsText) + ")</span>";
        }

        return "<span title=\"raw: " + Encode(raw.ToString("0.###############", Invariant)) + " ms\">" + main + "</span>";
    }

    private static string FormatBytes(double? value)
    {
        if (!value.HasValue)
        {
            return "<span class=\"muted\">n/a</span>";
        }

        var raw = value.Value;
        var main = FormatScientificWithUnit(raw, "B", includeSecondary: false);
        var binary = FormatBinaryBytes(raw);
        return "<span title=\"raw: " + Encode(raw.ToString("0.###############", Invariant)) + " B\">" +
               main + " (" + Encode(binary) + ")</span>";
    }

    private static string FormatScientificWithUnit(double value, string unit, bool includeSecondary)
    {
        var abs = Math.Abs(value);
        var useScientific = abs >= 1000.0 || (abs > 0 && abs < 0.01);
        if (!useScientific)
        {
            var plain = value.ToString("0.###", Invariant);
            return string.IsNullOrWhiteSpace(unit) ? Encode(plain) : Encode(plain + " " + unit);
        }

        var exponent = (int)Math.Floor(Math.Log10(abs));
        var mantissa = value / Math.Pow(10, exponent);
        var mantissaText = mantissa.ToString("0.###", Invariant);
        var scientific = Encode(mantissaText) + " &times; 10<sup>" + exponent.ToString(Invariant) + "</sup>";
        if (!string.IsNullOrWhiteSpace(unit))
        {
            scientific += " " + Encode(unit);
        }

        return scientific;
    }

    private static string FormatBinaryBytes(double bytes)
    {
        var abs = Math.Abs(bytes);
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        var unitIndex = 0;
        var scaled = abs;
        while (scaled >= 1024.0 && unitIndex < units.Length - 1)
        {
            scaled /= 1024.0;
            unitIndex++;
        }

        var signedScaled = bytes < 0 ? -scaled : scaled;
        var format = unitIndex == 0 ? "0" : "0.###";
        return signedScaled.ToString(format, Invariant) + " " + units[unitIndex];
    }

    private static string FormatMillisecondsAxis(double value)
    {
        return FormatAxisValue(value, "ms");
    }

    private static string FormatBytesAxis(double value)
    {
        return FormatAxisValue(value, "B");
    }

    private static string FormatAxisValue(double value, string unit)
    {
        var abs = Math.Abs(value);
        var formatted = abs >= 1000.0
            ? value.ToString("0.###e+0", Invariant)
            : value.ToString("0.###", Invariant);
        return formatted + " " + unit;
    }

    private static string SnapshotLabel(ComparisonSnapshot snapshot, int maxChars)
    {
        var label = snapshot.ComparisonSetId;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = snapshot.SnapshotTimestampUtc.ToString("MM-dd HH:mm", Invariant);
        }

        if (label!.Length <= maxChars)
        {
            return label;
        }

        return label[..(maxChars - 3)] + "...";
    }

    private static string DescribeRuntimeSemantics(string engineKey, ExperimentEngineSpec spec)
    {
        var hasNuget = !string.IsNullOrWhiteSpace(spec.Nuget);
        var isPolar = engineKey.Equals("polar-db", StringComparison.OrdinalIgnoreCase);

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

    private static bool ResolveEnabledFlag(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.GetArrayLength() > 0,
            JsonValueKind.Object => ResolveObjectEnabledFlag(element),
            _ => false
        };
    }

    private static bool ResolveObjectEnabledFlag(JsonElement element)
    {
        if (element.TryGetProperty("enabled", out var enabled))
        {
            if (enabled.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (enabled.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        if (element.TryGetProperty("experiments", out var experiments) &&
            experiments.ValueKind == JsonValueKind.Array)
        {
            return experiments.GetArrayLength() > 0;
        }

        return true;
    }

    private static string EngineColor(string engineKey)
    {
        return engineKey.ToLowerInvariant() switch
        {
            "polar-db" => "#0f6f9f",
            "sqlite" => "#a04f15",
            "synthetic" => "#5c3ea6",
            _ => "#2d7b63"
        };
    }

    private static string StripHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("<sup>", "^", StringComparison.OrdinalIgnoreCase)
            .Replace("</sup>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<span class=\"muted\">", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<span>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</span>", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string EncodeAttribute(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private sealed record ChartSeries(string Name, string Color, IReadOnlyList<double?> Values);
}

internal sealed record ExperimentIndexModel(
    ExperimentManifest Manifest,
    LatestEnginesComparisonArtifact? LatestEngines,
    LatestHistoryComparisonArtifact? LatestHistory,
    LatestOtherExperimentsComparisonArtifact? LatestOtherExperiments,
    IReadOnlyList<LocalAnalyzedSeriesResult> LocalAnalyzedSeries,
    IReadOnlyList<ArtifactFileLink> RawArtifacts,
    IReadOnlyList<ArtifactFileLink> AnalyzedArtifacts,
    IReadOnlyList<ArtifactFileLink> ComparisonArtifacts,
    DateTimeOffset GeneratedAtUtc);

internal sealed record ArtifactFileLink(
    string RelativePath,
    DateTimeOffset? LastWriteUtc);

