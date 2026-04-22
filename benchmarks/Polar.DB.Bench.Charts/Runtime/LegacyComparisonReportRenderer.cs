using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Renders legacy single-run comparison artifacts.
/// This is a fallback path when comparison-series files are not available.
/// </summary>
internal sealed class LegacyComparisonReportRenderer
{
    /// <summary>
    /// Renders legacy comparison artifacts as markdown table.
    /// </summary>
    public string BuildMarkdown(IReadOnlyList<CrossEngineComparisonResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cross-Engine Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("Legacy mode: comparison-series artifacts were not found, so this summary uses single-run comparison artifacts.");
        sb.AppendLine();
        sb.AppendLine("| ComparisonId | Experiment | Dataset | Fairness | Polar elapsed ms | SQLite elapsed ms | Polar load ms | SQLite load ms | Polar build ms | SQLite build ms | Polar reopen ms | SQLite reopen ms | Polar lookup ms | SQLite lookup ms | Polar total bytes | SQLite total bytes | Polar primary bytes | SQLite db bytes | Polar side bytes | SQLite side bytes | Polar semantic | SQLite semantic | Polar technical | SQLite technical |");
        sb.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- |");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var polar = FindEngine(comparison, "polar-db");
            var sqlite = FindEngine(comparison, "sqlite");

            sb.AppendLine(
                $"| {ReportFormatting.EscapeMarkdownCell(comparison.ComparisonId)} | {ReportFormatting.EscapeMarkdownCell(comparison.ExperimentKey)} | {ReportFormatting.EscapeMarkdownCell(comparison.DatasetProfileKey ?? string.Empty)} | {ReportFormatting.EscapeMarkdownCell(comparison.FairnessProfileKey ?? string.Empty)} | {ReportFormatting.FormatNumber(polar?.ElapsedMsSingleRun)} | {ReportFormatting.FormatNumber(sqlite?.ElapsedMsSingleRun)} | {ReportFormatting.FormatNumber(polar?.LoadMs)} | {ReportFormatting.FormatNumber(sqlite?.LoadMs)} | {ReportFormatting.FormatNumber(polar?.BuildMs)} | {ReportFormatting.FormatNumber(sqlite?.BuildMs)} | {ReportFormatting.FormatNumber(polar?.ReopenMs)} | {ReportFormatting.FormatNumber(sqlite?.ReopenMs)} | {ReportFormatting.FormatNumber(polar?.LookupMs)} | {ReportFormatting.FormatNumber(sqlite?.LookupMs)} | {ReportFormatting.FormatNumber(polar?.TotalArtifactBytes)} | {ReportFormatting.FormatNumber(sqlite?.TotalArtifactBytes)} | {ReportFormatting.FormatNumber(polar?.PrimaryArtifactBytes)} | {ReportFormatting.FormatNumber(sqlite?.PrimaryArtifactBytes)} | {ReportFormatting.FormatNumber(polar?.SideArtifactBytes)} | {ReportFormatting.FormatNumber(sqlite?.SideArtifactBytes)} | {ReportFormatting.FormatBool(polar?.SemanticSuccess)} | {ReportFormatting.FormatBool(sqlite?.SemanticSuccess)} | {ReportFormatting.FormatBool(polar?.TechnicalSuccess)} | {ReportFormatting.FormatBool(sqlite?.TechnicalSuccess)} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders legacy comparison artifacts as CSV rows.
    /// </summary>
    public string BuildCsv(IReadOnlyList<CrossEngineComparisonResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ComparisonId,ExperimentKey,DatasetProfileKey,FairnessProfileKey,PolarElapsedMsSingleRun,SqliteElapsedMsSingleRun,PolarLoadMs,SqliteLoadMs,PolarBuildMs,SqliteBuildMs,PolarReopenMs,SqliteReopenMs,PolarLookupMs,SqliteLookupMs,PolarTotalArtifactBytes,SqliteTotalArtifactBytes,PolarPrimaryArtifactBytes,SqlitePrimaryArtifactBytes,PolarSideArtifactBytes,SqliteSideArtifactBytes,PolarSemanticSuccess,SqliteSemanticSuccess,PolarTechnicalSuccess,SqliteTechnicalSuccess");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var polar = FindEngine(comparison, "polar-db");
            var sqlite = FindEngine(comparison, "sqlite");

            sb.AppendLine(
                $"{ReportFormatting.Csv(comparison.ComparisonId)}," +
                $"{ReportFormatting.Csv(comparison.ExperimentKey)}," +
                $"{ReportFormatting.Csv(comparison.DatasetProfileKey ?? string.Empty)}," +
                $"{ReportFormatting.Csv(comparison.FairnessProfileKey ?? string.Empty)}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.ElapsedMsSingleRun))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.ElapsedMsSingleRun))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.LoadMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.LoadMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.BuildMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.BuildMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.ReopenMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.ReopenMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.LookupMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.LookupMs))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.TotalArtifactBytes))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.TotalArtifactBytes))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.PrimaryArtifactBytes))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.PrimaryArtifactBytes))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(polar?.SideArtifactBytes))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatNumber(sqlite?.SideArtifactBytes))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatBool(polar?.SemanticSuccess))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatBool(sqlite?.SemanticSuccess))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatBool(polar?.TechnicalSuccess))}," +
                $"{ReportFormatting.Csv(ReportFormatting.FormatBool(sqlite?.TechnicalSuccess))}");
        }

        return sb.ToString();
    }

    private static CrossEngineComparisonEntry? FindEngine(CrossEngineComparisonResult comparison, string engineKey)
    {
        return comparison.Engines.FirstOrDefault(x => x.EngineKey.Equals(engineKey, StringComparison.OrdinalIgnoreCase));
    }
}
