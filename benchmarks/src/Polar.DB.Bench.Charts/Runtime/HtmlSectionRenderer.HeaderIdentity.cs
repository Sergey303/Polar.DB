#nullable enable
using System;
using System.Linq;
using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

internal static partial class HtmlSectionRenderer
{
    public static void AppendHeader(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"header\">");
        sb.AppendLine("  <h1>" + H(ReadString(model, "Manifest.Title") ?? ReadString(model, "Manifest.ExperimentKey") ?? "Experiment") + "</h1>");
        sb.AppendLine("  <p class=\"muted\">Experiment: " + Code(ReadString(model, "Manifest.ExperimentKey")) + "</p>");

        var description = ReadString(model, "Manifest.Description");
        if (!string.IsNullOrWhiteSpace(description)) sb.AppendLine("  <p>" + H(description) + "</p>");

        sb.AppendLine("  <p class=\"meta\">");
        sb.AppendLine("    Generated: <span class=\"mono\">" + H(ReadString(model, "GeneratedAtUtc") ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", Invariant)) + "</span>.");
        sb.AppendLine("    Missing metric values are rendered as <code>N/A</code>, never as zero.");
        sb.AppendLine("  </p>");
        sb.AppendLine("</section>");
    }

    public static void AppendIdentitySection(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"grid\">");
        AppendKeyValueCard(sb, "Dataset", new[]
        {
            ("Profile", Code(ReadString(model, "Manifest.Dataset.ProfileKey"))),
            ("Count", FormatGeneralNumber(ReadLong(model, "Manifest.Dataset.RecordCount"))),
            ("Seed", H(ReadString(model, "Manifest.Dataset.Seed") ?? "n/a")),
            ("Notes", H(ReadString(model, "Manifest.Dataset.Notes") ?? string.Empty))
        });

        AppendKeyValueCard(sb, "Workload", new[]
        {
            ("Type", Code(ReadString(model, "Manifest.Workload.WorkloadKey"))),
            ("Lookup", H(ReadString(model, "Manifest.Workload.LookupCount") ?? "n/a")),
            ("Batches", H(ReadString(model, "Manifest.Workload.BatchCount") ?? "n/a")),
            ("Batch size", H(ReadString(model, "Manifest.Workload.BatchSize") ?? "n/a")),
            ("Notes", H(ReadString(model, "Manifest.Workload.Notes") ?? string.Empty))
        });

        AppendKeyValueCard(sb, "Fairness", new[]
        {
            ("Profile", Code(ReadString(model, "Manifest.FairnessProfile.FairnessProfileKey"))),
            ("Notes", H(ReadString(model, "Manifest.FairnessProfile.Notes") ?? string.Empty)),
            ("Research", H(ReadString(model, "Manifest.ResearchQuestionId") ?? "n/a")),
            ("Hypothesis", H(ReadString(model, "Manifest.HypothesisId") ?? "n/a"))
        });
        sb.AppendLine("</section>");

        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Targets</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Target</th><th>Engine family</th><th>NuGet</th><th>Runtime semantics</th></tr></thead>");
        sb.AppendLine("    <tbody>");

        foreach (var targetEntry in Enumerate(ReadPath(model, "Manifest.Targets")).OrderBy(x => ReadString(x, "Key"), StringComparer.OrdinalIgnoreCase))
        {
            var targetKey = ReadString(targetEntry, "Key") ?? "unknown";
            var targetSpec = ReadPath(targetEntry, "Value");
            var engine = ReadString(targetSpec, "Engine") ?? "unknown";
            var nuget = ReadString(targetSpec, "Nuget") ?? "current/source";

            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + Code(targetKey) + "</td>");
            sb.AppendLine("        <td>" + Code(engine) + "</td>");
            sb.AppendLine("        <td>" + Code(nuget) + "</td>");
            sb.AppendLine("        <td>" + H(DescribeRuntimeSemantics(targetKey, targetSpec)) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</section>");
    }

    private static void AppendKeyValueCard(StringBuilder sb, string title, (string Key, string Value)[] rows)
    {
        sb.AppendLine("  <article class=\"card\">");
        sb.AppendLine("    <h2>" + H(title) + "</h2>");
        sb.AppendLine("    <table>");
        foreach (var row in rows)
        {
            sb.AppendLine("      <tr><th>" + H(row.Key) + "</th><td>" + row.Value + "</td></tr>");
        }
        sb.AppendLine("    </table>");
        sb.AppendLine("  </article>");
    }
}
