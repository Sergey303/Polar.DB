using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Renders stage4 comparison-series artifacts to markdown and CSV.
/// This renderer works with derived comparison artifacts, not with raw run files.
/// </summary>
internal sealed class SeriesComparisonReportRenderer
{
    /// <summary>
    /// Renders comparison-series artifacts as markdown table.
    /// </summary>
    public string BuildMarkdown(IReadOnlyList<CrossEngineComparisonSeriesResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cross-Target Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("This summary compares measured runs inside the same comparison set.");
        sb.AppendLine("Fairness profile means each target maps one shared intent (durability/performance balance) to engine-specific settings.");
        sb.AppendLine("Primary bytes are the main data file(s). Side bytes are WAL/state/index/other side artifacts.");
        sb.AppendLine("Technical success means run infrastructure completed. Semantic success means workload-level checks passed.");
        sb.AppendLine();
        sb.AppendLine("| ComparisonId | Set | Experiment | Experiment label | Dataset | Fairness | Target | Lookup batch avg | Measured runs | Elapsed ms (min/avg/med/max) | Load ms (min/avg/med/max) | Build ms (min/avg/med/max) | Reopen ms (min/avg/med/max) | Lookup ms (min/avg/med/max) | Total bytes (min/avg/med/max) | Primary bytes (min/avg/med/max) | Side bytes (min/avg/med/max) | Technical success | Semantic success |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | ---: | ---: | --- | --- | --- | --- | --- | --- | --- | --- | ---: | ---: |");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var experimentDisplayName = ReportFormatting.ExperimentDisplayName(comparison.ExperimentKey);
            foreach (var engine in comparison.EngineSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(
                    $"| {ReportFormatting.EscapeMarkdownCell(comparison.ComparisonId)} | {ReportFormatting.EscapeMarkdownCell(comparison.ComparisonSetId)} | {ReportFormatting.EscapeMarkdownCell(comparison.ExperimentKey)} | {ReportFormatting.EscapeMarkdownCell(experimentDisplayName)} | {ReportFormatting.EscapeMarkdownCell(comparison.DatasetProfileKey ?? string.Empty)} | {ReportFormatting.EscapeMarkdownCell(comparison.FairnessProfileKey ?? string.Empty)} | {ReportFormatting.EscapeMarkdownCell(engine.EngineKey)} | {ReportFormatting.FormatNumber(engine.LookupBatchCount?.Average)} | {engine.MeasuredRunCount} | {ReportFormatting.FormatStats(engine.ElapsedMs)} | {ReportFormatting.FormatStats(engine.LoadMs)} | {ReportFormatting.FormatStats(engine.BuildMs)} | {ReportFormatting.FormatStats(engine.ReopenMs)} | {ReportFormatting.FormatStats(engine.LookupMs)} | {ReportFormatting.FormatStats(engine.TotalArtifactBytes)} | {ReportFormatting.FormatStats(engine.PrimaryArtifactBytes)} | {ReportFormatting.FormatStats(engine.SideArtifactBytes)} | {engine.TechnicalSuccessCount}/{engine.MeasuredRunCount} | {engine.SemanticSuccessCount}/{engine.MeasuredRunCount} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Metric format is min/avg/median/max. If some runs miss a metric, a suffix like [n=2/3] shows available values.");
        sb.AppendLine();

        // Stability section
        AppendStabilitySection(sb, comparisons);

        sb.AppendLine("Comparison notes:");
        foreach (var note in comparisons
                     .OrderBy(x => x.TimestampUtc)
                     .SelectMany(x => x.Notes?.AsEnumerable() ?? Array.Empty<string>())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {note}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Renders a compact stability section with p95/p99/trimmedMean10/MAD/jitter%/outliers.
    /// </summary>
    private static void AppendStabilitySection(
        StringBuilder sb,
        IReadOnlyList<CrossEngineComparisonSeriesResult> comparisons)
    {
        var hasStability = comparisons
            .SelectMany(c => c.EngineSeries)
            .Any(e => HasStabilityData(e.ElapsedMs) || HasStabilityData(e.LoadMs) ||
                      HasStabilityData(e.BuildMs) || HasStabilityData(e.ReopenMs) ||
                      HasStabilityData(e.LookupMs));

        if (!hasStability)
        {
            return;
        }

        sb.AppendLine("## Stability");
        sb.AppendLine();
        sb.AppendLine("Compact format: p95/p99/trimmedMean10/MAD/jitter%/outliers.");
        sb.AppendLine("Empty cells mean the statistic could not be computed (too few samples or zero median).");
        sb.AppendLine();
        sb.AppendLine("| ComparisonId | Target | Elapsed stability | Load stability | Build stability | Reopen stability | Lookup stability |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            foreach (var engine in comparison.EngineSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(
                    $"| {ReportFormatting.EscapeMarkdownCell(comparison.ComparisonId)} | {ReportFormatting.EscapeMarkdownCell(engine.EngineKey)} | {ReportFormatting.FormatStability(engine.ElapsedMs)} | {ReportFormatting.FormatStability(engine.LoadMs)} | {ReportFormatting.FormatStability(engine.BuildMs)} | {ReportFormatting.FormatStability(engine.ReopenMs)} | {ReportFormatting.FormatStability(engine.LookupMs)} |");
            }
        }

        sb.AppendLine();
    }

    private static bool HasStabilityData(MetricSeriesStats stats)
    {
        return stats.P95.HasValue || stats.P99.HasValue ||
               stats.TrimmedMean10.HasValue || stats.Mad.HasValue ||
               stats.JitterRatio.HasValue || stats.OutlierCount.HasValue;
    }

    /// <summary>
    /// Renders comparison-series artifacts as CSV rows.
    /// </summary>
    public string BuildCsv(IReadOnlyList<CrossEngineComparisonSeriesResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ComparisonId,ComparisonSetId,ExperimentKey,ExperimentDisplayName,DatasetProfileKey,FairnessProfileKey,TargetKey,MeasuredRunCount,WarmupRunCount,TechnicalSuccessCount,SemanticSuccessCount,SemanticEvaluatedCount,ElapsedCount,ElapsedMissing,ElapsedMin,ElapsedMax,ElapsedAverage,ElapsedMedian,ElapsedP50,ElapsedP95,ElapsedP99,ElapsedTrimmedMean10,ElapsedMad,ElapsedJitterRatio,ElapsedOutlierCount,ElapsedOutlierPercent,LoadCount,LoadMissing,LoadMin,LoadMax,LoadAverage,LoadMedian,LoadP50,LoadP95,LoadP99,LoadTrimmedMean10,LoadMad,LoadJitterRatio,LoadOutlierCount,LoadOutlierPercent,BuildCount,BuildMissing,BuildMin,BuildMax,BuildAverage,BuildMedian,BuildP50,BuildP95,BuildP99,BuildTrimmedMean10,BuildMad,BuildJitterRatio,BuildOutlierCount,BuildOutlierPercent,ReopenCount,ReopenMissing,ReopenMin,ReopenMax,ReopenAverage,ReopenMedian,ReopenP50,ReopenP95,ReopenP99,ReopenTrimmedMean10,ReopenMad,ReopenJitterRatio,ReopenOutlierCount,ReopenOutlierPercent,LookupCount,LookupMissing,LookupMin,LookupMax,LookupAverage,LookupMedian,LookupP50,LookupP95,LookupP99,LookupTrimmedMean10,LookupMad,LookupJitterRatio,LookupOutlierCount,LookupOutlierPercent,LookupBatchCount,LookupBatchMissing,LookupBatchMin,LookupBatchMax,LookupBatchAverage,LookupBatchMedian,TotalBytesCount,TotalBytesMissing,TotalBytesMin,TotalBytesMax,TotalBytesAverage,TotalBytesMedian,TotalBytesP50,TotalBytesP95,TotalBytesP99,TotalBytesTrimmedMean10,TotalBytesMad,TotalBytesJitterRatio,TotalBytesOutlierCount,TotalBytesOutlierPercent,PrimaryBytesCount,PrimaryBytesMissing,PrimaryBytesMin,PrimaryBytesMax,PrimaryBytesAverage,PrimaryBytesMedian,PrimaryBytesP50,PrimaryBytesP95,PrimaryBytesP99,PrimaryBytesTrimmedMean10,PrimaryBytesMad,PrimaryBytesJitterRatio,PrimaryBytesOutlierCount,PrimaryBytesOutlierPercent,SideBytesCount,SideBytesMissing,SideBytesMin,SideBytesMax,SideBytesAverage,SideBytesMedian,SideBytesP50,SideBytesP95,SideBytesP99,SideBytesTrimmedMean10,SideBytesMad,SideBytesJitterRatio,SideBytesOutlierCount,SideBytesOutlierPercent");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var experimentDisplayName = ReportFormatting.ExperimentDisplayName(comparison.ExperimentKey);
            foreach (var engine in comparison.EngineSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(
                    $"{ReportFormatting.Csv(comparison.ComparisonId)}," +
                    $"{ReportFormatting.Csv(comparison.ComparisonSetId)}," +
                    $"{ReportFormatting.Csv(comparison.ExperimentKey)}," +
                    $"{ReportFormatting.Csv(experimentDisplayName)}," +
                    $"{ReportFormatting.Csv(comparison.DatasetProfileKey ?? string.Empty)}," +
                    $"{ReportFormatting.Csv(comparison.FairnessProfileKey ?? string.Empty)}," +
                    $"{ReportFormatting.Csv(engine.EngineKey)}," +
                    $"{engine.MeasuredRunCount}," +
                    $"{engine.WarmupRunCount}," +
                    $"{engine.TechnicalSuccessCount}," +
                    $"{engine.SemanticSuccessCount}," +
                    $"{engine.SemanticEvaluatedCount}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.ElapsedMs)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.ElapsedMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.LoadMs)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.LoadMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.BuildMs)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.BuildMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.ReopenMs)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.ReopenMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.LookupMs)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.LookupMs)}," +
                    $"{FormatStatsCsvOptional(engine.LookupBatchCount)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.TotalArtifactBytes)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.TotalArtifactBytes)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.PrimaryArtifactBytes)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.PrimaryArtifactBytes)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.SideArtifactBytes)}," +
                    $"{ReportFormatting.FormatStabilityCsv(engine.SideArtifactBytes)}");
            }
        }

        return sb.ToString();
    }

    private static string FormatStatsCsvOptional(MetricSeriesStats? stats)
    {
        return stats is null
            ? "0,0,,,,"
            : ReportFormatting.FormatStatsCsv(stats);
    }
}
