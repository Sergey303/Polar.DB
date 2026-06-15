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
        var bestRowsSec = rows.Max(row => RowsPerSecond(row.Engine.Rows, row.Stats.TrimmedMean));
        var bestHdd = engines.Min(engine => engine.ArtifactBytes);
        var bestPrivate = engines.Min(engine => engine.ResourcesAfter.PrivateBytes);
        var bestWorking = engines.Min(engine => engine.ResourcesAfter.WorkingSetBytes);
        var bestHeap = engines.Min(engine => engine.ResourcesAfter.ManagedBytes);
        var bestAvailable = engines.Max(engine => engine.ResourcesAfter.AvailableMemoryBytes);

        builder.AppendLine("<h3>Timing and resources</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Status</th><th>Total median</th><th>Total p95</th><th>Total trimmed</th><th>Rows/sec by trimmed</th><th>Rows</th><th>HDD</th><th>Private RAM</th><th>Working set</th><th>Managed heap</th><th>Available RAM</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Escape(engine.Status) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append(BenchmarkReportFormat.HigherBetterCell(RowsPerSecond(engine.Rows, row.Stats.TrimmedMean), bestRowsSec, ""));
            builder.Append("<td>" + engine.Rows + "</td>");
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ArtifactBytes, bestHdd));
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ResourcesAfter.PrivateBytes, bestPrivate));
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ResourcesAfter.WorkingSetBytes, bestWorking));
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ResourcesAfter.ManagedBytes, bestHeap));
            builder.Append(BenchmarkReportFormat.HigherBetterCell(engine.ResourcesAfter.AvailableMemoryBytes, bestAvailable, " B") + "</tr>");
        }
        builder.AppendLine("</table>");
    }

    private static void AppendBuildBreakdown(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        var rows = engines.Select(e => (Engine: e, Build: BenchmarkStats.From(e.BuildSamplesMs ?? Array.Empty<double>()),
            Flush: BenchmarkStats.From(e.FlushSamplesMs ?? Array.Empty<double>()), Total: BenchmarkStats.From(e.SamplesMs))).ToArray();
        var bestBuildMedian = rows.Min(row => row.Build.Median);
        var bestBuildP95 = rows.Min(row => row.Build.P95);
        var bestBuildTrimmed = rows.Min(row => row.Build.TrimmedMean);
        var bestBuildRowsSec = rows.Max(row => RowsPerSecond(row.Engine.Rows, row.Build.TrimmedMean));
        var bestFlushMedian = rows.Min(row => row.Flush.Median);
        var bestFlushP95 = rows.Min(row => row.Flush.P95);
        var bestFlushTrimmed = rows.Min(row => row.Flush.TrimmedMean);
        var bestTotalMedian = rows.Min(row => row.Total.Median);

        builder.AppendLine("<h3>Build stage breakdown</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Build median</th><th>Build p95</th><th>Build trimmed</th><th>Build rows/sec by trimmed</th><th>Flush median</th><th>Flush p95</th><th>Flush trimmed</th><th>Total median</th><th>Build share</th></tr>");
        foreach (var row in rows)
        {
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(row.Engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Build.Median, bestBuildMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Build.P95, bestBuildP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Build.TrimmedMean, bestBuildTrimmed, " ms"));
            builder.Append(BenchmarkReportFormat.HigherBetterCell(RowsPerSecond(row.Engine.Rows, row.Build.TrimmedMean), bestBuildRowsSec, ""));
            builder.Append(BenchmarkReportFormat.Cell(row.Flush.Median, bestFlushMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Flush.P95, bestFlushP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Flush.TrimmedMean, bestFlushTrimmed, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Total.Median, bestTotalMedian, " ms"));
            builder.Append("<td>" + BenchmarkReportFormat.Number(Share(row.Build.Median, row.Total.Median)) + "%</td></tr>");
        }
        builder.AppendLine("</table>");
    }

    private static void AppendPrimaryBuildInternals(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        var rows = engines.Where(e => e.PrimaryBuildStages != null).ToArray();
        var bestScan = rows.Min(e => BenchmarkStats.From(e.PrimaryBuildStages!.ScanMs).Median);
        var bestArrays = rows.Min(e => BenchmarkStats.From(e.PrimaryBuildStages!.ToArrayMs).Median);
        var bestSort = rows.Min(e => BenchmarkStats.From(e.PrimaryBuildStages!.SortMs).Median);
        var bestHash = rows.Min(e => BenchmarkStats.From(e.PrimaryBuildStages!.WriteHashKeysMs).Median);
        var bestOffsets = rows.Min(e => BenchmarkStats.From(e.PrimaryBuildStages!.WriteOffsetsMs).Median);
        var bestGc = rows.Min(e => BenchmarkStats.From(e.PrimaryBuildStages!.GcMs).Median);
        var bestTotal = rows.Min(e => BenchmarkStats.From(e.PrimaryBuildStages!.ProfileTotalMs).Median);

        builder.AppendLine("<h3>Polar.DB primary index build internals</h3>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Scan/extract</th><th>To arrays</th><th>Sort</th><th>Write hash keys</th><th>Write offsets</th><th>GC</th><th>Profile total</th></tr>");
        foreach (var engine in rows)
        {
            var s = engine.PrimaryBuildStages!;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(StageCell(s.ScanMs, bestScan));
            builder.Append(StageCell(s.ToArrayMs, bestArrays));
            builder.Append(StageCell(s.SortMs, bestSort));
            builder.Append(StageCell(s.WriteHashKeysMs, bestHash));
            builder.Append(StageCell(s.WriteOffsetsMs, bestOffsets));
            builder.Append(StageCell(s.GcMs, bestGc));
            builder.Append(StageCell(s.ProfileTotalMs, bestTotal));
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

    private static string StageCell(IReadOnlyList<double> values, double best) =>
        BenchmarkReportFormat.Cell(BenchmarkStats.From(values).Median, best, " ms");

    private static double RowsPerSecond(long rows, double trimmedMs) =>
        trimmedMs <= 0 || double.IsNaN(trimmedMs) ? double.NaN : rows * 1000.0 / trimmedMs;

    private static double Share(double part, double total) =>
        double.IsNaN(part) || total <= 0 ? double.NaN : part * 100.0 / total;
}
