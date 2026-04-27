using System;
using System.Collections.Generic;
using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Builds the main human-readable experiment page (index.html).
/// This is the entry renderer that delegates to smaller helper classes:
/// - HtmlSectionRenderer for HTML sections
/// - ChartRenderer for inline SVG charts
/// - NumberFormatter for number formatting
/// The page is fully static HTML with inline SVG charts and links to machine-readable artifacts.
/// </summary>
internal sealed class ExperimentIndexRenderer
{
    /// <summary>
    /// Builds the full HTML page for one experiment.
    /// </summary>
    public string BuildHtml(ExperimentIndexModel model)
    {
        var sb = new StringBuilder(capacity: 64 * 1024);
        AppendPageStart(sb, model);
        HtmlSectionRenderer.AppendHeader(sb, model);
        HtmlSectionRenderer.AppendIdentitySection(sb, model);
        HtmlSectionRenderer.AppendLatestEnginesSection(sb, model);
        HtmlSectionRenderer.AppendThematicMetricSections(sb, model);
        HtmlSectionRenderer.AppendHistorySection(sb, model);
        HtmlSectionRenderer.AppendOtherExperimentsSection(sb, model);
        HtmlSectionRenderer.AppendArtifactsSection(sb, model);
        AppendPageEnd(sb);
        return sb.ToString();
    }

    private static void AppendPageStart(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine($"  <title>{NumberFormatter.HtmlEncode(model.Manifest.Title)} - Benchmark Experiment</title>");
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
        sb.AppendLine("    .chart-title { padding: 10px 12px 0; font-weight: 650; color: #20252b; }");
        sb.AppendLine("    .metric-cell { min-width: 118px; }");
        sb.AppendLine("    .metric-main { font-weight: 560; }");
        sb.AppendLine("    .metric-ratio { margin-top: 3px; color: var(--muted); font-size: 0.78rem; font-family: var(--mono); }");
        sb.AppendLine("    .metric-best { background: #eef9f1; }");
        sb.AppendLine("    .metric-best .metric-ratio { color: var(--good); font-weight: 700; }");
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
    }

    private static void AppendPageEnd(StringBuilder sb)
    {
        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
    }
}

/// <summary>
/// Data model for the experiment index page.
/// All data is pre-loaded by ChartsArtifactLoader before rendering.
/// </summary>
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

/// <summary>
/// A link to one artifact file on disk, relative to the experiment folder.
/// </summary>
internal sealed record ArtifactFileLink(
    string RelativePath,
    DateTimeOffset? LastWriteUtc);
