using System.Text;

namespace PolarDbBenchmarks;

internal static class BenchmarkReportTables
{
    public static void AppendTiming(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        AppendMainTiming(builder, engines);
        if (engines.Any(engine => engine.BuildSamplesMs != null)) AppendBuildBreakdown(builder, engines);
        if (engines.Any(engine => engine.PrimaryBuildStages != null)) AppendPrimaryBuildInternals(builder, engines);
    }

    private static void AppendMainTiming(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        var rows = engines.Select(engine => (Engine: engine, Stats: BenchmarkStats.From(engine.SamplesMs))).ToArray();
        var bestMedian = rows.Min(row => row.Stats.Median);
        var bestP95 = rows.Min(row => row.Stats.P95);
        var bestTrimmed = rows.Min(row => row.Stats.TrimmedMean);
        var bestHdd = engines.Min(engine => engine.ArtifactBytes);
        var bestPrivate = engines.Min(engine => engine.ResourcesAfter.PrivateBytes);
        var bestWorking = engines.Min(engine => engine.ResourcesAfter.WorkingSetBytes);

        builder.AppendLine("<h3>Timing and resources</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Status</th><th>Total median</th><th>Total p95</th><th>Total trimmed</th><th>Rows</th><th>HDD</th><th>Private RAM</th><th>Working set</th><th>Managed heap</th><th>Available RAM</th></tr>");
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

    private static void AppendBuildBreakdown(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        builder.AppendLine("<h3>Build stage breakdown</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Build median</th><th>Build p95</th><th>Flush median</th><th>Flush p95</th><th>Total median</th><th>Build share</th></tr>");
        foreach (var engine in engines)
        {
            var build = BenchmarkStats.From(engine.BuildSamplesMs ?? Array.Empty<double>());
            var flush = BenchmarkStats.From(engine.FlushSamplesMs ?? Array.Empty<double>());
            var total = BenchmarkStats.From(engine.SamplesMs);
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(build.Median) + " ms</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(build.P95) + " ms</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(flush.Median) + " ms</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(flush.P95) + " ms</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(total.Median) + " ms</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(Share(build.Median, total.Median)) + "%</td></tr>");
        }
        builder.AppendLine("</table>");
    }

    private static void AppendPrimaryBuildInternals(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        builder.AppendLine("<h3>Polar.DB primary index build internals</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Scan/extract</th><th>To arrays</th><th>Sort</th><th>Write hash keys</th><th>Write offsets</th><th>GC</th><th>Profile total</th></tr>");
        foreach (var engine in engines.Where(engine => engine.PrimaryBuildStages != null))
        {
            var s = engine.PrimaryBuildStages!;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(Cell(s.ScanMs));
            builder.Append(Cell(s.ToArrayMs));
            builder.Append(Cell(s.SortMs));
            builder.Append(Cell(s.WriteHashKeysMs));
            builder.Append(Cell(s.WriteOffsetsMs));
            builder.Append(Cell(s.GcMs));
            builder.Append(Cell(s.ProfileTotalMs));
            builder.Append("</tr>");
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

    public static void AppendCorrectness(StringBuilder builder, QueryResult expected, IReadOnlyList<EngineResult> engines)
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

    private static string Cell(IReadOnlyList<double> values) =>
        "<td>" + BenchmarkReportFormat.Number(BenchmarkStats.From(values).Median) + " ms</td>";

    private static double Share(double part, double total) =>
        double.IsNaN(part) || total <= 0 ? double.NaN : part * 100.0 / total;
}
