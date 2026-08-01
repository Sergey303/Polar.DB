using System.Text;

namespace PolarDbBenchmarks;

internal static class BenchmarkReport
{
    public static string Render(
        ExperimentOptions options,
        IReadOnlyList<BenchmarkRunResult> runs,
        BenchmarkRunManifest manifest)
    {
        var builder = Header(options, manifest);
        foreach (var run in runs)
            AppendLifecycleRun(builder, run);
        AppendCommonNotes(builder, manifest);
        return Finish(builder);
    }

    public static string RenderLookup(
        ExperimentOptions options,
        IReadOnlyList<LookupRunResult> runs,
        BenchmarkRunManifest manifest)
    {
        var builder = Header(options, manifest);
        builder.AppendLine("<p>Lookup reports contain after-reopen-without-explicit-warmup and explicit-file-and-query-warmup phases.</p>");
        builder.AppendLine("<p>Batch metrics are throughput-oriented. Single-query latency metrics measure individual queries.</p>");
        foreach (var run in runs)
            AppendLookupRun(builder, run);
        AppendCommonNotes(builder, manifest);
        builder.AppendLine("<p>Reopen and worker-process startup are excluded from measured lookup time.</p>");
        return Finish(builder);
    }

    private static StringBuilder Header(ExperimentOptions options, BenchmarkRunManifest manifest)
    {
        var env = manifest.Coordinator;
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        builder.AppendLine("<title>" + BenchmarkReportFormat.Escape(options.ExperimentId) + "</title>");
        builder.AppendLine("<style>" + BenchmarkReportFormat.Css() + "</style>");
        builder.AppendLine("</head><body><h1>" + BenchmarkReportFormat.Escape(options.Title) + "</h1>");
        if (!env.PublicationReady)
        {
            builder.AppendLine("<p class=\"warn\"><strong>NON-PUBLICATION RUN:</strong> benchmark binaries were not confirmed as an optimized Release build. Do not use timings in a paper.</p>");
        }
        builder.AppendLine("<p><b>Experiment:</b> " + BenchmarkReportFormat.Escape(options.ExperimentId) + "</p>");
        builder.AppendLine("<p><b>Run ID:</b> " + BenchmarkReportFormat.Escape(manifest.RunId) + "</p>");
        builder.AppendLine("<p><b>Started UTC:</b> " + BenchmarkReportFormat.Escape(manifest.StartedUtc.ToString("O")) + "</p>");
        var gitState = !env.GitStatusKnown
            ? " (working-tree status unavailable)"
            : env.GitDirty
                ? " <strong>(tracked files modified)</strong>"
                : " (clean tracked tree)";
        builder.AppendLine("<p><b>Git commit:</b> " + BenchmarkReportFormat.Escape(env.CommitSha) +
            gitState + "</p>");
        builder.AppendLine("<p><b>Build:</b> " + BenchmarkReportFormat.Escape(env.BuildConfiguration) +
            "; <b>optimizations disabled:</b> " + env.OptimizationsDisabled +
            "; <b>publication ready:</b> " + env.PublicationReady + "</p>");
        builder.AppendLine("<p><b>Runtime settings:</b> tiered compilation " +
            BenchmarkReportFormat.Escape(env.TieredCompilationSetting) +
            "; tiered PGO " + BenchmarkReportFormat.Escape(env.TieredPgoSetting) +
            "; ReadyToRun " + BenchmarkReportFormat.Escape(env.ReadyToRunSetting) + "</p>");
        builder.AppendLine("<p><b>Runtime:</b> " + BenchmarkReportFormat.Escape(env.FrameworkDescription) +
            "; <b>OS:</b> " + BenchmarkReportFormat.Escape(env.OsDescription) +
            "; <b>process architecture:</b> " + BenchmarkReportFormat.Escape(env.ProcessArchitecture) + "</p>");
        builder.AppendLine("<p><b>CPU:</b> " + BenchmarkReportFormat.Escape(env.CpuDescription) +
            "; <b>logical processors:</b> " + env.ProcessorCount +
            "; <b>server GC:</b> " + env.ServerGc + "</p>");
        builder.AppendLine("<p><b>Row counts:</b> " +
            string.Join(", ", options.RowCounts.Select(count => BenchmarkReportFormat.Long(count))) + "</p>");
        builder.AppendLine("<p><a href=\"" + BenchmarkReportFormat.Escape(options.ExperimentId) +
            ".manifest.json\">environment manifest</a> · <a href=\"" +
            BenchmarkReportFormat.Escape(options.ExperimentId) +
            ".raw.json\">raw JSON</a> · <a href=\"" +
            BenchmarkReportFormat.Escape(options.ExperimentId) +
            ".raw.csv\">raw CSV</a></p>");
        builder.AppendLine("<p>Each engine was measured in a separate child process. Green cells mark winners only for directly comparable columns.</p>");

        builder.AppendLine("<h2>Engine processes</h2>");
        builder.AppendLine("<table><tr><th>Engine</th><th>PID</th><th>Captured UTC</th><th>Runtime</th><th>Build</th><th>Publication ready</th><th>Commit</th><th>Dirty</th></tr>");
        foreach (var process in manifest.EngineProcesses)
        {
            builder.AppendLine("<tr><td>" + BenchmarkReportFormat.Escape(process.Engine ?? process.ProcessRole) +
                "</td><td>" + process.ProcessId +
                "</td><td>" + BenchmarkReportFormat.Escape(process.CapturedUtc.ToString("O")) +
                "</td><td>" + BenchmarkReportFormat.Escape(process.FrameworkDescription) +
                "</td><td>" + BenchmarkReportFormat.Escape(process.BuildConfiguration) +
                "</td><td class=\"" + (process.PublicationReady ? "ok" : "warn") + "\">" + process.PublicationReady +
                "</td><td>" + BenchmarkReportFormat.Escape(process.CommitSha) +
                "</td><td>" + (process.GitStatusKnown ? process.GitDirty.ToString() : "unknown") + "</td></tr>");
        }
        builder.AppendLine("</table>");
        return builder;
    }

    private static void AppendLifecycleRun(StringBuilder builder, BenchmarkRunResult run)
    {
        builder.AppendLine("<h2>Rows: " + BenchmarkReportFormat.Long(run.SetupRows) + "</h2>");
        BenchmarkReportTables.AppendTiming(builder, run.Engines);
        BenchmarkReportTables.AppendMemoryPressure(builder, run.Engines);
        BenchmarkReportTables.AppendCorrectness(builder, run.Expected, run.Engines);
    }

    private static void AppendLookupRun(StringBuilder builder, LookupRunResult run)
    {
        builder.AppendLine("<h2>Rows: " + BenchmarkReportFormat.Long(run.SetupRows) + "</h2>");
        foreach (var phase in run.Phases)
            SearchBenchmarkReportTables.AppendPhase(builder, phase);
    }

    private static void AppendCommonNotes(StringBuilder builder, BenchmarkRunManifest manifest)
    {
        builder.AppendLine("<h2>Protocol notes</h2>");
        builder.AppendLine("<p>Correctness ignores materialized row order but checks row count and row values.</p>");
        builder.AppendLine("<p>Lookup batch and latency key sets have independent expected row counts and checksums.</p>");
        builder.AppendLine("<p>RAM values are process-level snapshots from the corresponding isolated engine worker.</p>");
        builder.AppendLine("<p>Available RAM is detected from the operating system when possible.</p>");
        builder.AppendLine("<p><b>Reopen:</b> " + BenchmarkReportFormat.Escape(manifest.ReopenDefinition) + "</p>");
        builder.AppendLine("<p><b>Volatile mutation:</b> " + BenchmarkReportFormat.Escape(manifest.VolatileMutationDefinition) + "</p>");
        builder.AppendLine("<p><b>Durable mutation:</b> " + BenchmarkReportFormat.Escape(manifest.DurableMutationDefinition) + "</p>");
        builder.AppendLine("<p>Raw per-sample values and resolved lookup plans are retained in JSON and CSV; the HTML contains derived summaries only.</p>");
    }

    private static string Finish(StringBuilder builder)
    {
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }
}
