using System.Text;

namespace PolarDbBenchmarks;

internal static class SearchBenchmarkReportTables
{
    public static void AppendPhase(StringBuilder builder, LookupPhaseResult phase)
    {
        builder.AppendLine("<h3>" + BenchmarkReportFormat.Escape(phase.Name) + "</h3>");
        AppendBatch(builder, phase);
        AppendLatency(builder, phase);
        BenchmarkReportTables.AppendCorrectness(builder, phase.Expected, phase.Engines.Select(Convert).ToArray());
    }

    private static void AppendBatch(StringBuilder builder, LookupPhaseResult phase)
    {
        var rows = phase.Engines.Select(e => (Engine: e, Stats: BenchmarkStats.From(e.BatchAvgSamplesMs))).ToArray();
        var bestMedian = rows.Min(row => row.Stats.Median);
        var bestP95 = rows.Min(row => row.Stats.P95);
        var bestTrimmed = rows.Min(row => row.Stats.TrimmedMean);

        builder.AppendLine("<h4>Batch throughput</h4>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Median batch avg ms/query</th><th>P95 batch avg</th><th>Trimmed batch avg</th><th>Samples</th><th>Batch queries</th><th>Queries</th><th>Rows total</th><th>Rows/query</th><th>Rows/sec</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append("<td>" + engine.BatchAvgSamplesMs.Count + "</td>");
            builder.Append("<td>" + BatchQueries(engine) + "</td>");
            builder.Append("<td>" + engine.BatchQueries + "</td><td>" + engine.BatchRows + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(RowsPerQuery(engine)) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(RowsPerSecond(engine)) + "</td></tr>");
        }
        builder.AppendLine("</table>");
    }

    private static void AppendLatency(StringBuilder builder, LookupPhaseResult phase)
    {
        var rows = phase.Engines.Select(e => (Engine: e, Stats: BenchmarkStats.From(e.LatencySamplesMs))).ToArray();
        var bestMedian = rows.Min(row => row.Stats.Median);
        var bestP95 = rows.Min(row => row.Stats.P95);

        builder.AppendLine("<h4>Single-query latency</h4>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Median latency</th><th>P95 latency</th><th>Max latency</th><th>Latency samples</th><th>HDD</th><th>Private RAM</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append("<td>" + BenchmarkReportFormat.Number(row.Stats.Max) + " ms</td>");
            builder.Append("<td>" + engine.LatencySamplesMs.Count + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Bytes(engine.ArtifactBytes) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Bytes(engine.ResourcesAfter.PrivateBytes) + "</td></tr>");
        }
        builder.AppendLine("</table>");
    }

    private static double RowsPerQuery(LookupEngineResult engine) =>
        engine.BatchQueries == 0 ? 0 : (double)engine.BatchRows / engine.BatchQueries;

    private static long BatchQueries(LookupEngineResult engine) =>
        engine.BatchAvgSamplesMs.Count == 0 ? 0 : engine.BatchQueries / engine.BatchAvgSamplesMs.Count;

    private static double RowsPerSecond(LookupEngineResult engine)
    {
        if (engine.BatchAvgSamplesMs.Count == 0 || engine.BatchQueries == 0) return 0;
        var lookupsPerSample = (double)engine.BatchQueries / engine.BatchAvgSamplesMs.Count;
        var totalMs = engine.BatchAvgSamplesMs.Sum() * lookupsPerSample;
        return totalMs <= 0 ? 0 : engine.BatchRows / (totalMs / 1000.0);
    }

    private static EngineResult Convert(LookupEngineResult engine) =>
        new(engine.Engine, engine.Status, engine.BatchAvgSamplesMs, engine.BatchRows, engine.BatchChecksum,
            engine.ArtifactBytes, engine.ResourcesBefore, engine.ResourcesAfter);
}
