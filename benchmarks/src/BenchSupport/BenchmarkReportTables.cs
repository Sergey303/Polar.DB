using System.Text;

namespace PolarDbBenchmarks;

internal static class BenchmarkReportTables
{
    public static void AppendTiming(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        var rows = engines.Select(engine => (Engine: engine, Stats: BenchmarkStats.From(engine.SamplesMs))).ToArray();
        var bestMedian = rows.Min(row => row.Stats.Median);
        var bestP95 = rows.Min(row => row.Stats.P95);
        var bestTrimmed = rows.Min(row => row.Stats.TrimmedMean);
        var bestHdd = engines.Min(engine => engine.ArtifactBytes);
        var bestPrivate = engines.Min(engine => engine.ResourcesAfter.PrivateBytes);
        var bestWorking = engines.Min(engine => engine.ResourcesAfter.WorkingSetBytes);

        builder.AppendLine("<h3>Timing and resources</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Status</th><th>Median</th><th>P95</th><th>Trimmed mean</th><th>Rows</th><th>HDD</th><th>Private RAM</th><th>Working set</th><th>Managed heap</th><th>Available RAM</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Escape(engine.Status) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append("<td>" + engine.Rows + "</td>");
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ArtifactBytes, bestHdd));
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ResourcesAfter.PrivateBytes, bestPrivate));
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ResourcesAfter.WorkingSetBytes, bestWorking));
            builder.Append("<td>" + BenchmarkReportFormat.Bytes(engine.ResourcesAfter.ManagedBytes) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Bytes(engine.ResourcesAfter.AvailableMemoryBytes) + "</td></tr>");
        }

        builder.AppendLine("</table>");
    }

    public static void AppendMemoryPressure(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        builder.AppendLine("<h3>Memory pressure</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>HDD / available RAM</th><th>Private / available RAM</th><th>Working set / available RAM</th></tr>");
        foreach (var engine in engines)
        {
            var available = engine.ResourcesAfter.AvailableMemoryBytes;
            builder.AppendLine("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>" +
                BenchmarkReportFormat.RatioCell(engine.ArtifactBytes, available) +
                BenchmarkReportFormat.RatioCell(engine.ResourcesAfter.PrivateBytes, available) +
                BenchmarkReportFormat.RatioCell(engine.ResourcesAfter.WorkingSetBytes, available) + "</tr>");
        }

        builder.AppendLine("</table>");
    }

    public static void AppendCorrectness(
        StringBuilder builder,
        QueryResult expected,
        IReadOnlyList<EngineResult> engines)
    {
        builder.AppendLine("<h3>Correctness</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Rows</th><th>Checksum</th><th>Status</th></tr>");
        builder.AppendLine("<tr><td>expected</td><td>" + expected.Rows +
            "</td><td>" + expected.Checksum + "</td><td class=\"ok\">Baseline</td></tr>");

        foreach (var engine in engines)
        {
            var ok = engine.Rows == expected.Rows && engine.Checksum == expected.Checksum;
            builder.AppendLine("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td><td>" +
                engine.Rows + "</td><td>" + engine.Checksum + "</td><td class=\"" +
                (ok ? "ok" : "warn") + "\">" + (ok ? "OK" : "Mismatch") + "</td></tr>");
        }

        builder.AppendLine("</table>");
    }
}
