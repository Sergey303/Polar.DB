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
        sb.AppendLine("# Cross-Engine Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("This summary compares measured runs inside the same comparison set.");
        sb.AppendLine("Fairness profile means both engines map one shared intent (durability/performance balance) to engine-specific settings.");
        sb.AppendLine("Primary bytes are the main data file(s). Side bytes are WAL/state/index/other side artifacts.");
        sb.AppendLine("Technical success means run infrastructure completed. Semantic success means workload-level checks passed.");
        sb.AppendLine();
        sb.AppendLine("| ComparisonId | Set | Experiment | Dataset | Fairness | Engine | Measured runs | Elapsed ms (min/avg/med/max) | Load ms (min/avg/med/max) | Build ms (min/avg/med/max) | Reopen ms (min/avg/med/max) | Lookup ms (min/avg/med/max) | Total bytes (min/avg/med/max) | Primary bytes (min/avg/med/max) | Side bytes (min/avg/med/max) | Technical success | Semantic success |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- | --- | --- | ---: | ---: |");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            foreach (var engine in comparison.EngineSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(
                    $"| {ReportFormatting.EscapeMarkdownCell(comparison.ComparisonId)} | {ReportFormatting.EscapeMarkdownCell(comparison.ComparisonSetId)} | {ReportFormatting.EscapeMarkdownCell(comparison.ExperimentKey)} | {ReportFormatting.EscapeMarkdownCell(comparison.DatasetProfileKey ?? string.Empty)} | {ReportFormatting.EscapeMarkdownCell(comparison.FairnessProfileKey ?? string.Empty)} | {ReportFormatting.EscapeMarkdownCell(engine.EngineKey)} | {engine.MeasuredRunCount} | {ReportFormatting.FormatStats(engine.ElapsedMs)} | {ReportFormatting.FormatStats(engine.LoadMs)} | {ReportFormatting.FormatStats(engine.BuildMs)} | {ReportFormatting.FormatStats(engine.ReopenMs)} | {ReportFormatting.FormatStats(engine.LookupMs)} | {ReportFormatting.FormatStats(engine.TotalArtifactBytes)} | {ReportFormatting.FormatStats(engine.PrimaryArtifactBytes)} | {ReportFormatting.FormatStats(engine.SideArtifactBytes)} | {engine.TechnicalSuccessCount}/{engine.MeasuredRunCount} | {engine.SemanticSuccessCount}/{engine.MeasuredRunCount} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Metric format is min/avg/median/max. If some runs miss a metric, a suffix like [n=2/3] shows available values.");
        return sb.ToString();
    }

    /// <summary>
    /// Renders comparison-series artifacts as CSV rows.
    /// </summary>
    public string BuildCsv(IReadOnlyList<CrossEngineComparisonSeriesResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ComparisonId,ComparisonSetId,ExperimentKey,DatasetProfileKey,FairnessProfileKey,EngineKey,MeasuredRunCount,WarmupRunCount,TechnicalSuccessCount,SemanticSuccessCount,SemanticEvaluatedCount,ElapsedCount,ElapsedMissing,ElapsedMin,ElapsedMax,ElapsedAverage,ElapsedMedian,LoadCount,LoadMissing,LoadMin,LoadMax,LoadAverage,LoadMedian,BuildCount,BuildMissing,BuildMin,BuildMax,BuildAverage,BuildMedian,ReopenCount,ReopenMissing,ReopenMin,ReopenMax,ReopenAverage,ReopenMedian,LookupCount,LookupMissing,LookupMin,LookupMax,LookupAverage,LookupMedian,TotalBytesCount,TotalBytesMissing,TotalBytesMin,TotalBytesMax,TotalBytesAverage,TotalBytesMedian,PrimaryBytesCount,PrimaryBytesMissing,PrimaryBytesMin,PrimaryBytesMax,PrimaryBytesAverage,PrimaryBytesMedian,SideBytesCount,SideBytesMissing,SideBytesMin,SideBytesMax,SideBytesAverage,SideBytesMedian");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            foreach (var engine in comparison.EngineSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(
                    $"{ReportFormatting.Csv(comparison.ComparisonId)}," +
                    $"{ReportFormatting.Csv(comparison.ComparisonSetId)}," +
                    $"{ReportFormatting.Csv(comparison.ExperimentKey)}," +
                    $"{ReportFormatting.Csv(comparison.DatasetProfileKey ?? string.Empty)}," +
                    $"{ReportFormatting.Csv(comparison.FairnessProfileKey ?? string.Empty)}," +
                    $"{ReportFormatting.Csv(engine.EngineKey)}," +
                    $"{engine.MeasuredRunCount}," +
                    $"{engine.WarmupRunCount}," +
                    $"{engine.TechnicalSuccessCount}," +
                    $"{engine.SemanticSuccessCount}," +
                    $"{engine.SemanticEvaluatedCount}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.ElapsedMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.LoadMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.BuildMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.ReopenMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.LookupMs)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.TotalArtifactBytes)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.PrimaryArtifactBytes)}," +
                    $"{ReportFormatting.FormatStatsCsv(engine.SideArtifactBytes)}");
            }
        }

        return sb.ToString();
    }
}
