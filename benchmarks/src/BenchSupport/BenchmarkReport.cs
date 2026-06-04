using System.Text;

namespace PolarDbBenchmarks;

internal static class BenchmarkReport
{
    public static string Render(ExperimentOptions options, IReadOnlyList<BenchmarkRunResult> runs)
    {
        var builder = Header(options);
        foreach (var run in runs)
            AppendLifecycleRun(builder, run);
        AppendCommonNotes(builder);
        return Finish(builder);
    }

    public static string RenderLookup(ExperimentOptions options, IReadOnlyList<LookupRunResult> runs)
    {
        var builder = Header(options);
        builder.AppendLine("<p>Lookup reports contain two phases: cold after reopen and hot after file/cache warmup.</p>");
        foreach (var run in runs)
            AppendLookupRun(builder, run);
        AppendCommonNotes(builder);
        builder.AppendLine("<p>Reopen is excluded from measured lookup time.</p>");
        return Finish(builder);
    }

    private static StringBuilder Header(ExperimentOptions options)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        builder.AppendLine("<title>" + BenchmarkReportFormat.Escape(options.ExperimentId) + "</title>");
        builder.AppendLine("<style>" + BenchmarkReportFormat.Css() + "</style>");
        builder.AppendLine("</head><body><h1>" + BenchmarkReportFormat.Escape(options.Title) + "</h1>");
        builder.AppendLine("<p><b>Experiment:</b> " + BenchmarkReportFormat.Escape(options.ExperimentId) + "</p>");
        builder.AppendLine("<p><b>Row orders:</b> " + string.Join(", ", options.RowCounts) + "</p>");
        builder.AppendLine("<p>Lower is better. Green cells are column winners.</p>");
        return builder;
    }

    private static void AppendLifecycleRun(StringBuilder builder, BenchmarkRunResult run)
    {
        builder.AppendLine("<h2>Rows: " + run.SetupRows + "</h2>");
        BenchmarkReportTables.AppendTiming(builder, run.Engines);
        BenchmarkReportTables.AppendMemoryPressure(builder, run.Engines);
        BenchmarkReportTables.AppendCorrectness(builder, run.Expected, run.Engines);
    }

    private static void AppendLookupRun(StringBuilder builder, LookupRunResult run)
    {
        builder.AppendLine("<h2>Rows: " + run.SetupRows + "</h2>");
        foreach (var phase in run.Phases)
            SearchBenchmarkReportTables.AppendPhase(builder, phase);
    }

    private static void AppendCommonNotes(StringBuilder builder)
    {
        builder.AppendLine("<h2>Notes</h2>");
        builder.AppendLine("<p>Correctness ignores materialized row order but checks row count and row values.</p>");
        builder.AppendLine("<p>RAM values are process-level snapshots after each engine run.</p>");
        builder.AppendLine("<p>Available RAM is detected from the operating system when possible.</p>");
    }

    private static string Finish(StringBuilder builder)
    {
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }
}
