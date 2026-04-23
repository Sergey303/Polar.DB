using System.Globalization;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Builds HTML sections for the experiment index page.
/// Each method renders one card/section of the page.
/// </summary>
internal static class HtmlSectionRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

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
        sb.AppendLine("  </p>");
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Renders the identity section: dataset, workload, fairness, and engines tables.
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
        sb.AppendLine("  <h2>Engines</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Engine</th><th>Spec</th><th>Runtime semantics</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var (engineKey, engineSpec) in model.Manifest.Engines.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var spec = string.IsNullOrWhiteSpace(engineSpec.Nuget)
                ? "{}"
                : "{ \"nuget\": \"" + NumberFormatter.HtmlEncode(engineSpec.Nuget!) + "\" }";
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(engineKey) + "</code></td>");
            sb.AppendLine("        <td><code>" + spec + "</code></td>");
            sb.AppendLine("        <td>" + NumberFormatter.HtmlEncode(DescribeRuntimeSemantics(engineKey, engineSpec)) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Renders the latest engine comparison section with KPI cards, table, and charts.
    /// </summary>
    public static void AppendLatestEnginesSection(System.Text.StringBuilder sb, ExperimentIndexModel model)
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
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Comparison set</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.ComparisonSetId ?? "legacy/latest") + "</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Timestamp</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.SnapshotTimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", Invariant)) + " UTC</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Dataset</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.DatasetProfileKey ?? "mixed") + "</span></div>");
        sb.AppendLine("    <div class=\"kpi\"><span class=\"label\">Fairness</span><span class=\"val mono\">" + NumberFormatter.HtmlEncode(snapshot.FairnessProfileKey ?? "mixed") + "</span></div>");
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
            sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(engine.EngineKey) + "</code></td>");
            sb.AppendLine("        <td>" + engine.MeasuredRunCount + "</td>");
            sb.AppendLine("        <td>" + engine.TechnicalSuccessCount + "/" + engine.MeasuredRunCount + "</td>");
            sb.AppendLine("        <td>" + engine.SemanticSuccessCount + "/" + engine.SemanticEvaluatedCount + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(engine.ElapsedMs.Median) + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(engine.LoadMs.Median) + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(engine.BuildMs.Median) + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(engine.ReopenMs.Median) + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(engine.LookupMs.Median) + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatBytes(engine.TotalArtifactBytes.Median) + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatBytes(engine.PrimaryArtifactBytes.Median) + "</td>");
            sb.AppendLine("        <td>" + NumberFormatter.FormatBytes(engine.SideArtifactBytes.Median) + "</td>");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(ChartRenderer.RenderGroupedBarChart(
            "Phase Breakdown (latest series, median ms)",
            new[] { "Load", "Build", "Reopen", "Lookup" },
            snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new ChartSeries(
                    item.EngineKey,
                    ChartRenderer.EngineColor(item.EngineKey),
                    new double?[]
                    {
                        item.LoadMs.Median,
                        item.BuildMs.Median,
                        item.ReopenMs.Median,
                        item.LookupMs.Median
                    }))
                .ToArray(),
            "ms",
            NumberFormatter.FormatMillisecondsAxis));
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"chart-wrap\">");
        sb.AppendLine(ChartRenderer.RenderGroupedBarChart(
            "Artifact Sizes (latest series, median bytes)",
            new[] { "Primary", "Side", "Total" },
            snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new ChartSeries(
                    item.EngineKey,
                    ChartRenderer.EngineColor(item.EngineKey),
                    new double?[]
                    {
                        item.PrimaryArtifactBytes.Median,
                        item.SideArtifactBytes.Median,
                        item.TotalArtifactBytes.Median
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

        // Use the simplified bool from manifest directly.
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
            sb.AppendLine("<th>Elapsed median: <code>" + NumberFormatter.HtmlEncode(engine) + "</code></th>");
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
                sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(series?.ElapsedMs.Median) + "</td>");
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
        sb.AppendLine("    <thead><tr><th>Experiment</th><th>Set</th><th>Timestamp</th><th>Dataset</th><th>Fairness</th><th>Elapsed median per engine</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var snapshot in others)
        {
            var summary = string.Join("; ", snapshot.EngineSeries
                .OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.EngineKey}: {NumberFormatter.StripHtml(NumberFormatter.FormatMilliseconds(item.ElapsedMs.Median))}"));
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
            sb.AppendLine("    <thead><tr><th>Engine</th><th>Set</th><th>Elapsed median</th><th>Total bytes median</th><th>Measured</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in model.LocalAnalyzedSeries.OrderBy(x => x.EngineKey, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(item.EngineKey) + "</code></td>");
                sb.AppendLine("        <td><code>" + NumberFormatter.HtmlEncode(item.ComparisonSetId ?? "legacy/latest") + "</code></td>");
                sb.AppendLine("        <td>" + NumberFormatter.FormatMilliseconds(item.ElapsedMs.Median) + "</td>");
                sb.AppendLine("        <td>" + NumberFormatter.FormatBytes(item.TotalArtifactBytes.Median) + "</td>");
                sb.AppendLine("        <td>" + item.MeasuredRunCount + "</td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        sb.AppendLine("</section>");
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
    /// Describes runtime semantics for an engine spec in human-readable form.
    /// </summary>
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
}
