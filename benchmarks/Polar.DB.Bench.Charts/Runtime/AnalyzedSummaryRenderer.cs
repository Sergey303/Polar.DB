using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Renders summaries for analyzed policy results.
/// Input is already-evaluated artifacts; output is markdown and CSV text.
/// </summary>
internal sealed class AnalyzedSummaryRenderer
{
    /// <summary>
    /// Renders analyzed results as markdown summary table.
    /// </summary>
    public string BuildMarkdown(IReadOnlyList<AnalyzedResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Benchmark Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("| RunId | Status | Policy | Baseline |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var result in results)
        {
            sb.AppendLine($"| {ReportFormatting.EscapeMarkdownCell(result.RunId)} | {ReportFormatting.EscapeMarkdownCell(result.OverallStatus)} | {ReportFormatting.EscapeMarkdownCell(result.PolicyId ?? string.Empty)} | {ReportFormatting.EscapeMarkdownCell(result.BaselineId ?? string.Empty)} |");
        }

        sb.AppendLine();
        sb.AppendLine("Current charts output is markdown and CSV summary only.");
        return sb.ToString();
    }

    /// <summary>
    /// Renders analyzed results as CSV rows.
    /// </summary>
    public string BuildCsv(IReadOnlyList<AnalyzedResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RunId,OverallStatus,PolicyId,BaselineId");
        foreach (var result in results)
        {
            sb.AppendLine(
                $"{ReportFormatting.Csv(result.RunId)}," +
                $"{ReportFormatting.Csv(result.OverallStatus)}," +
                $"{ReportFormatting.Csv(result.PolicyId ?? string.Empty)}," +
                $"{ReportFormatting.Csv(result.BaselineId ?? string.Empty)}");
        }

        return sb.ToString();
    }
}
