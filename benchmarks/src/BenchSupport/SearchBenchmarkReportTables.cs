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
        var bestRowsSec = rows.Max(row => RowsPerSecondByTrimmed(row.Engine, row.Stats.TrimmedMean));

        builder.AppendLine("<h4>Batch throughput</h4>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Median batch avg ms/query</th><th>P95 batch avg</th><th>Trimmed batch avg</th><th>Rows/sec by trimmed</th><th>Batches count</th><th>Queries/batch</th><th>Total queries</th><th>Returned rows</th><th>Returned rows/query</th><th>Rows/sec actual</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append(BenchmarkReportFormat.HigherBetterCell(RowsPerSecondByTrimmed(engine, row.Stats.TrimmedMean), bestRowsSec, ""));
            builder.Append("<td>" + engine.BatchAvgSamplesMs.Count + "</td>");
            builder.Append("<td>" + QueriesPerBatch(engine) + "</td>");
            builder.Append("<td>" + engine.BatchQueries + "</td><td>" + engine.BatchRows + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(RowsPerQuery(engine)) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(RowsPerSecondActual(engine)) + "</td></tr>");
        }
        builder.AppendLine("</table>");
    }

    private static void AppendLatency(StringBuilder builder, LookupPhaseResult phase)
    {
        var rows = phase.Engines.Select(e => (Engine: e, Stats: BenchmarkStats.From(e.LatencySamplesMs))).ToArray();
        var bestMedian = rows.Min(row => row.Stats.Median);
        var bestP95 = rows.Min(row => row.Stats.P95);
        var bestTrimmed = rows.Min(row => row.Stats.TrimmedMean);
        var bestMax = rows.Min(row => row.Stats.Max);
        var bestRowsSec = rows.Max(row => LatencyRowsPerSecondByTrimmed(row.Engine, row.Stats.TrimmedMean));
        var bestHdd = rows.Min(row => row.Engine.ArtifactBytes);
        var bestPrivate = rows.Min(row => row.Engine.ResourcesAfter.PrivateBytes);

        builder.AppendLine("<h4>Single-query latency</h4>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Median latency</th><th>P95 latency</th><th>Trimmed latency</th><th>Rows/sec by trimmed</th><th>Max latency</th><th>Latency samples</th><th>HDD</th><th>Private RAM</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append(BenchmarkReportFormat.HigherBetterCell(LatencyRowsPerSecondByTrimmed(engine, row.Stats.TrimmedMean), bestRowsSec, ""));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Max, bestMax, " ms"));
            builder.Append("<td>" + engine.LatencySamplesMs.Count + "</td>");
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ArtifactBytes, bestHdd));
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ResourcesAfter.PrivateBytes, bestPrivate) + "</tr>");
        }
        builder.AppendLine("</table>");
    }

    private static double RowsPerQuery(LookupEngineResult engine) =>
        engine.BatchQueries == 0 ? 0 : (double)engine.BatchRows / engine.BatchQueries;

    private static long QueriesPerBatch(LookupEngineResult engine) =>
        engine.BatchAvgSamplesMs.Count == 0 ? 0 : engine.BatchQueries / engine.BatchAvgSamplesMs.Count;

    private static double RowsPerSecondByTrimmed(LookupEngineResult engine, double trimmedMsPerQuery) =>
        trimmedMsPerQuery <= 0 || double.IsNaN(trimmedMsPerQuery) ? double.NaN :
            RowsPerQuery(engine) * 1000.0 / trimmedMsPerQuery;

    private static double LatencyRowsPerSecondByTrimmed(LookupEngineResult engine, double trimmedMs)
    {
        if (trimmedMs <= 0 || double.IsNaN(trimmedMs)) return double.NaN;
        var rowsPerQuery = engine.LatencySamplesMs.Count == 0 ? 0 : (double)engine.LatencyRows / engine.LatencySamplesMs.Count;
        return rowsPerQuery * 1000.0 / trimmedMs;
    }

    private static double RowsPerSecondActual(LookupEngineResult engine)
    {
        if (engine.BatchAvgSamplesMs.Count == 0 || engine.BatchQueries == 0) return 0;
        var queriesPerSample = (double)engine.BatchQueries / engine.BatchAvgSamplesMs.Count;
        var totalMs = engine.BatchAvgSamplesMs.Sum() * queriesPerSample;
        return totalMs <= 0 ? 0 : engine.BatchRows / (totalMs / 1000.0);
    }

    private static EngineResult Convert(LookupEngineResult engine) =>
        new(engine.Engine, engine.Status, engine.BatchAvgSamplesMs, engine.BatchRows, engine.BatchChecksum,
            engine.ArtifactBytes, engine.ResourcesBefore, engine.ResourcesAfter);
}
