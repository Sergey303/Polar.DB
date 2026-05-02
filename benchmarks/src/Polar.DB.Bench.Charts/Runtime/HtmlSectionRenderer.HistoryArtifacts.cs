#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

internal static partial class HtmlSectionRenderer
{
    public static void AppendHistorySection(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>History</h2>");

        if (ReadBool(model, "Manifest.Compare.History") == false)
        {
            sb.AppendLine("  <p class=\"status-off\">Disabled by experiment <code>compare.history</code> flag.</p>");
            sb.AppendLine("</section>");
            return;
        }

        var latestHistory = ReadPath(model, "LatestHistory");
        if (latestHistory is null)
        {
            sb.AppendLine("  <p class=\"muted\">Comparison artifact <code>comparisons/latest-history.json</code> is not available yet.</p>");
            sb.AppendLine("</section>");
            return;
        }

        if (ReadBool(latestHistory, "Enabled") == false)
        {
            sb.AppendLine("  <p class=\"status-off\">History artifact is currently marked disabled.</p>");
            AppendNotes(sb, ReadPath(latestHistory, "Notes"));
            sb.AppendLine("</section>");
            return;
        }

        var snapshots = Enumerate(ReadPath(latestHistory, "Snapshots"))
            .OrderBy(x => ReadString(x, "SnapshotTimestampUtc"), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (snapshots.Length == 0)
        {
            sb.AppendLine("  <p class=\"muted\">No successful measured history snapshots were found.</p>");
            AppendNotes(sb, ReadPath(latestHistory, "Notes"));
            sb.AppendLine("</section>");
            return;
        }

        var engineKeys = snapshots
            .SelectMany(snapshot => Enumerate(ReadPath(snapshot, "EngineSeries")))
            .Select(engine => ReadString(engine, "EngineKey") ?? "unknown")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>#</th><th>Set</th><th>Timestamp</th>");
        foreach (var engineKey in engineKeys) sb.AppendLine("<th>Elapsed tm: " + Code(engineKey) + "</th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("    <tbody>");
        for (var i = 0; i < snapshots.Length; i++)
        {
            var snapshot = snapshots[i];
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + (i + 1).ToString(Invariant) + "</td>");
            sb.AppendLine("        <td>" + Code(ReadString(snapshot, "ComparisonSetId") ?? "legacy") + "</td>");
            sb.AppendLine("        <td class=\"mono\">" + H(ReadString(snapshot, "SnapshotTimestampUtc") ?? "n/a") + "</td>");
            foreach (var engineKey in engineKeys)
            {
                var series = Enumerate(ReadPath(snapshot, "EngineSeries")).FirstOrDefault(x => string.Equals(ReadString(x, "EngineKey"), engineKey, StringComparison.OrdinalIgnoreCase));
                sb.AppendLine("        <td>" + FormatMilliseconds(GetMetricTm(series, "ElapsedMs")) + "</td>");
            }
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        AppendNotes(sb, ReadPath(latestHistory, "Notes"));
        AppendExpectations(sb, ReadPath(latestHistory, "DerivedExpectations"));
        sb.AppendLine("</section>");
    }

    public static void AppendOtherExperimentsSection(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Context With Other Experiments</h2>");

        if (ReadBool(model, "Manifest.Compare.OtherExperiments") == false)
        {
            sb.AppendLine("  <p class=\"status-off\">Disabled by experiment <code>compare.otherExperiments</code> flag.</p>");
            sb.AppendLine("</section>");
            return;
        }

        var latestOther = ReadPath(model, "LatestOtherExperiments");
        if (latestOther is null)
        {
            sb.AppendLine("  <p class=\"muted\">Comparison artifact <code>comparisons/latest-other-experiments.json</code> is not available yet.</p>");
            sb.AppendLine("</section>");
            return;
        }

        if (ReadBool(latestOther, "Enabled") == false)
        {
            sb.AppendLine("  <p class=\"status-off\">Other-experiments artifact is currently marked disabled.</p>");
            AppendNotes(sb, ReadPath(latestOther, "Notes"));
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <p class=\"muted\">This section is context-only and does not claim strict apples-to-apples equivalence.</p>");
        var others = Enumerate(ReadPath(latestOther, "OtherExperimentSnapshots"))
            .OrderBy(x => ReadString(x, "ExperimentKey"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => ReadString(x, "SnapshotTimestampUtc"), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (others.Length == 0)
        {
            sb.AppendLine("  <p class=\"muted\">No resolved external snapshots were found for auto-discovered experiments.</p>");
            AppendNotes(sb, ReadPath(latestOther, "Notes"));
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Experiment</th><th>Set</th><th>Timestamp</th><th>Dataset</th><th>Fairness</th><th>Elapsed tm per target</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var snapshot in others)
        {
            var summary = string.Join("; ", Enumerate(ReadPath(snapshot, "EngineSeries"))
                .OrderBy(x => ReadString(x, "EngineKey"), StringComparer.OrdinalIgnoreCase)
                .Select(x => (ReadString(x, "EngineKey") ?? "unknown") + ": " + FormatMilliseconds(GetMetricTm(x, "ElapsedMs"))));

            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td>" + Code(ReadString(snapshot, "ExperimentKey") ?? "unknown") + "</td>");
            sb.AppendLine("        <td>" + Code(ReadString(snapshot, "ComparisonSetId") ?? "legacy") + "</td>");
            sb.AppendLine("        <td class=\"mono\">" + H(ReadString(snapshot, "SnapshotTimestampUtc") ?? "n/a") + "</td>");
            sb.AppendLine("        <td>" + Code(ReadString(snapshot, "DatasetProfileKey") ?? "mixed") + "</td>");
            sb.AppendLine("        <td>" + Code(ReadString(snapshot, "FairnessProfileKey") ?? "mixed") + "</td>");
            sb.AppendLine("        <td>" + H(summary) + "</td>");
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        AppendNotes(sb, ReadPath(latestOther, "Notes"));
        AppendExpectations(sb, ReadPath(latestOther, "DerivedExpectations"));
        sb.AppendLine("</section>");
    }

    public static void AppendArtifactsSection(StringBuilder sb, ExperimentIndexModel model)
    {
        sb.AppendLine("<section class=\"card wide\">");
        sb.AppendLine("  <h2>Machine-Readable Artifacts</h2>");
        sb.AppendLine("  <p class=\"muted small\">These links point to raw facts and derived JSON artifacts inside this experiment folder.</p>");

        AppendArtifactList(sb, "Latest Raw Files", ReadPath(model, "RawArtifacts"), 24);
        AppendArtifactList(sb, "Analyzed Files", ReadPath(model, "AnalyzedArtifacts"), 48);
        AppendArtifactList(sb, "Comparison Files", ReadPath(model, "ComparisonArtifacts"), 48);

        var local = Enumerate(ReadPath(model, "LocalAnalyzedSeries"))
            .OrderBy(x => ReadString(x, "EngineKey"), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (local.Length > 0)
        {
            sb.AppendLine("  <h3>Local Analyzed Snapshot</h3>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead><tr><th>Target</th><th>Set</th><th>Elapsed tm</th><th>Total bytes p50</th><th>Measured</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in local)
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine("        <td>" + Code(ReadString(item, "EngineKey") ?? "unknown") + "</td>");
                sb.AppendLine("        <td>" + Code(ReadString(item, "ComparisonSetId") ?? "legacy/latest") + "</td>");
                sb.AppendLine("        <td>" + FormatMilliseconds(GetMetricTm(item, "ElapsedMs")) + "</td>");
                sb.AppendLine("        <td>" + FormatBytes(GetMetricP50(item, "TotalArtifactBytes")) + "</td>");
                sb.AppendLine("        <td>" + FormatGeneralNumber(ReadInt(item, "MeasuredRunCount")) + "</td>");
                sb.AppendLine("      </tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        sb.AppendLine("</section>");
    }

    private static void AppendArtifactList(StringBuilder sb, string title, object? artifacts, int maxRows)
    {
        var items = Enumerate(artifacts).Take(maxRows).ToArray();
        sb.AppendLine("  <h3>" + H(title) + "</h3>");
        if (items.Length == 0)
        {
            sb.AppendLine("  <p class=\"muted\">No artifacts found.</p>");
            return;
        }

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Path</th><th>Bytes</th><th>Notes</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var item in items)
        {
            var path = ReadString(item, "RelativePath") ?? ReadString(item, "Path") ?? ReadString(item, "FullName") ?? item.ToString() ?? string.Empty;
            var bytes = ReadLong(item, "Bytes") ?? ReadLong(item, "Length");
            var notes = ReadString(item, "Notes") ?? string.Empty;
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + H(path.Replace('\\', '/')) + "</code></td>");
            sb.AppendLine("        <td>" + FormatBytes(bytes.HasValue ? bytes.Value : null) + "</td>");
            sb.AppendLine("        <td>" + H(notes) + "</td>");
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }
}
