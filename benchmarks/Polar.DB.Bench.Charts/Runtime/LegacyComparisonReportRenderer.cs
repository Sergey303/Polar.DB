using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Renders legacy single-run comparison artifacts.
/// This is a fallback path when comparison-series files are not available.
/// Shows all targets dynamically instead of hardcoding polar-db/sqlite columns.
/// </summary>
internal sealed class LegacyComparisonReportRenderer
{
    /// <summary>
    /// Renders legacy comparison artifacts as markdown table.
    /// </summary>
    public string BuildMarkdown(IReadOnlyList<CrossEngineComparisonResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cross-Target Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("Legacy mode: comparison-series artifacts were not found, so this summary uses single-run comparison artifacts.");
        sb.AppendLine();

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var engineKeys = comparison.Engines
                .Select(e => e.EngineKey)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Build dynamic header columns per engine.
            var headerCols = string.Join(" | ", engineKeys.SelectMany(k => new[]
            {
                $"{k} elapsed ms", $"{k} load ms", $"{k} build ms",
                $"{k} reopen ms", $"{k} lookup ms",
                $"{k} total bytes", $"{k} primary bytes", $"{k} side bytes",
                $"{k} semantic", $"{k} technical"
            }));
            sb.AppendLine($"| ComparisonId | Experiment | Dataset | Fairness | {headerCols} |");
            var alignCols = string.Join(" | ", engineKeys.SelectMany(_ => new[] { "---:", "---:", "---:", "---:", "---:", "---:", "---:", "---:", "---:", "---:" }));
            sb.AppendLine($"| --- | --- | --- | --- | {alignCols} |");

            var engineValues = engineKeys.SelectMany(k =>
            {
                var e = FindEngine(comparison, k);
                return new[]
                {
                    ReportFormatting.FormatNumber(e?.ElapsedMsSingleRun),
                    ReportFormatting.FormatNumber(e?.LoadMs),
                    ReportFormatting.FormatNumber(e?.BuildMs),
                    ReportFormatting.FormatNumber(e?.ReopenMs),
                    ReportFormatting.FormatNumber(e?.LookupMs),
                    ReportFormatting.FormatNumber(e?.TotalArtifactBytes),
                    ReportFormatting.FormatNumber(e?.PrimaryArtifactBytes),
                    ReportFormatting.FormatNumber(e?.SideArtifactBytes),
                    ReportFormatting.FormatBool(e?.SemanticSuccess),
                    ReportFormatting.FormatBool(e?.TechnicalSuccess)
                };
            });

            sb.AppendLine(
                $"| {ReportFormatting.EscapeMarkdownCell(comparison.ComparisonId)} | {ReportFormatting.EscapeMarkdownCell(comparison.ExperimentKey)} | {ReportFormatting.EscapeMarkdownCell(comparison.DatasetProfileKey ?? string.Empty)} | {ReportFormatting.EscapeMarkdownCell(comparison.FairnessProfileKey ?? string.Empty)} | {string.Join(" | ", engineValues)} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders legacy comparison artifacts as CSV rows.
    /// </summary>
    public string BuildCsv(IReadOnlyList<CrossEngineComparisonResult> comparisons)
    {
        var sb = new StringBuilder();

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var engineKeys = comparison.Engines
                .Select(e => e.EngineKey)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var headerCols = string.Join(",", engineKeys.SelectMany(k => new[]
            {
                $"{k}ElapsedMsSingleRun", $"{k}LoadMs", $"{k}BuildMs",
                $"{k}ReopenMs", $"{k}LookupMs",
                $"{k}TotalArtifactBytes", $"{k}PrimaryArtifactBytes", $"{k}SideArtifactBytes",
                $"{k}SemanticSuccess", $"{k}TechnicalSuccess"
            }));
            sb.AppendLine($"ComparisonId,ExperimentKey,DatasetProfileKey,FairnessProfileKey,{headerCols}");

            var engineValues = engineKeys.SelectMany(k =>
            {
                var e = FindEngine(comparison, k);
                return new[]
                {
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.ElapsedMsSingleRun)),
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.LoadMs)),
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.BuildMs)),
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.ReopenMs)),
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.LookupMs)),
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.TotalArtifactBytes)),
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.PrimaryArtifactBytes)),
                    ReportFormatting.Csv(ReportFormatting.FormatNumber(e?.SideArtifactBytes)),
                    ReportFormatting.Csv(ReportFormatting.FormatBool(e?.SemanticSuccess)),
                    ReportFormatting.Csv(ReportFormatting.FormatBool(e?.TechnicalSuccess))
                };
            });

            sb.AppendLine(
                $"{ReportFormatting.Csv(comparison.ComparisonId)}," +
                $"{ReportFormatting.Csv(comparison.ExperimentKey)}," +
                $"{ReportFormatting.Csv(comparison.DatasetProfileKey ?? string.Empty)}," +
                $"{ReportFormatting.Csv(comparison.FairnessProfileKey ?? string.Empty)}," +
                string.Join(",", engineValues));
        }

        return sb.ToString();
    }

    private static CrossEngineComparisonEntry? FindEngine(CrossEngineComparisonResult comparison, string engineKey)
    {
        return comparison.Engines.FirstOrDefault(x => x.EngineKey.Equals(engineKey, StringComparison.OrdinalIgnoreCase));
    }
}
