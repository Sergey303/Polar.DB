using System.Text;

namespace PolarDbBenchmarks;

internal static class BenchmarkReport
{
    public static string Render(ExperimentOptions options, IReadOnlyList<BenchmarkRunResult> runs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        builder.AppendLine("<title>" + BenchmarkReportFormat.Escape(options.ExperimentId) + "</title>");
        builder.AppendLine("<style>" + BenchmarkReportFormat.Css() + "</style>");
        builder.AppendLine("</head><body><h1>" + BenchmarkReportFormat.Escape(options.Title) + "</h1>");
        builder.AppendLine("<p><b>Experiment:</b> " + BenchmarkReportFormat.Escape(options.ExperimentId) + "</p>");
        builder.AppendLine("<p><b>Row orders:</b> " + string.Join(", ", options.RowCounts) +
            "; <b>warmup:</b> " + options.WarmupOps + "; <b>measured:</b> " + options.MeasuredOps + "</p>");
        builder.AppendLine("<p>Lower is better. Green cells are column winners; other cells show how many times worse they are.</p>");

        foreach (var run in runs)
            AppendRun(builder, run);

        builder.AppendLine("<h2>Notes</h2>");
        builder.AppendLine("<p>Correctness ignores materialized row order but still checks row count and row values.</p>");
        builder.AppendLine("<p>RAM values are process-level snapshots after each engine run. They include benchmark harness overhead and the generated dataset.</p>");
        builder.AppendLine("<p>Available RAM is detected from the operating system when possible: Windows GlobalMemoryStatusEx, Linux /proc/meminfo, otherwise .NET GC fallback.</p>");
        builder.AppendLine("<p>HDD is the total size of files produced by the engine for the current row order.</p>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static void AppendRun(StringBuilder builder, BenchmarkRunResult run)
    {
        builder.AppendLine("<h2>Rows: " + run.SetupRows + "</h2>");
        BenchmarkReportTables.AppendTiming(builder, run.Engines);
        BenchmarkReportTables.AppendMemoryPressure(builder, run.Engines);
        BenchmarkReportTables.AppendCorrectness(builder, run.Expected, run.Engines);
    }
}
