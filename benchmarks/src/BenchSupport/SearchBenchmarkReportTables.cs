using System.Text;

namespace PolarDbBenchmarks;

internal static class SearchBenchmarkReportTables
{
    public static void AppendPhase(StringBuilder builder, LookupPhaseResult phase)
    {
        builder.AppendLine("<h3>" + BenchmarkReportFormat.Escape(phase.Name) + "</h3>");
        AppendPlan(builder, phase.Plan);
        AppendBatch(builder, phase);
        AppendLatency(builder, phase);
        AppendCorrectness(
            builder,
            "Batch correctness",
            phase.ExpectedBatch,
            phase.Engines,
            engine => engine.BatchRows,
            engine => engine.BatchChecksum);
        AppendCorrectness(
            builder,
            "Latency correctness",
            phase.ExpectedLatency,
            phase.Engines,
            engine => engine.LatencyRows,
            engine => engine.LatencyChecksum);
    }

    private static void AppendPlan(StringBuilder builder, LookupPlanManifest plan)
    {
        builder.AppendLine("<h4>Resolved sampling plan</h4>");
        builder.AppendLine("<table><tr><th>File warmup</th><th>Warmup queries</th><th>Measured batches</th><th>Queries/batch</th><th>Batch queries</th><th>Latency samples</th><th>Total measured queries</th></tr>");
        builder.AppendLine("<tr><td>" + plan.FileWarmup +
            "</td><td>" + BenchmarkReportFormat.Long(plan.WarmupQueries) +
            "</td><td>" + BenchmarkReportFormat.Long(plan.MeasuredBatches) +
            "</td><td>" + BenchmarkReportFormat.Long(plan.QueriesPerBatch) +
            "</td><td>" + BenchmarkReportFormat.Long(plan.BatchQueries) +
            "</td><td>" + BenchmarkReportFormat.Long(plan.LatencySamples) +
            "</td><td>" + BenchmarkReportFormat.Long(plan.TotalMeasuredQueries) + "</td></tr>");
        builder.AppendLine("</table>");
    }

    private static void AppendBatch(StringBuilder builder, LookupPhaseResult phase)
    {
        var rows = phase.Engines.Select(e => (Engine: e, Stats: BenchmarkStats.From(e.BatchAvgSamplesMs))).ToArray();
        var bestMedian = rows.Min(row => row.Stats.Median);
        var bestP95 = rows.Min(row => row.Stats.P95);
        var bestTrimmed = rows.Min(row => row.Stats.TrimmedMean);

        builder.AppendLine("<h4>Batch throughput</h4>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Median batch avg ms/query</th><th>P95 batch avg</th><th>Trimmed batch avg</th><th>Batches count</th><th>Queries/batch</th><th>Total queries</th><th>Returned rows</th><th>Returned rows/query</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append("<td>" + BenchmarkReportFormat.Long(engine.BatchAvgSamplesMs.Count) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Long(QueriesPerBatch(engine)) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Long(engine.BatchQueries) + "</td><td>" + BenchmarkReportFormat.Long(engine.BatchRows) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(RowsPerQuery(engine)) + "</td></tr>");
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
        var bestHdd = rows.Min(row => row.Engine.ArtifactBytes);
        var bestPrivate = rows.Min(row => row.Engine.ResourcesAfter.PrivateBytes);

        builder.AppendLine("<h4>Single-query latency</h4>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Median latency</th><th>P95 latency</th><th>Trimmed latency</th><th>Max latency</th><th>Latency samples</th><th>HDD</th><th>Private RAM</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Max, bestMax, " ms"));
            builder.Append("<td>" + BenchmarkReportFormat.Long(engine.LatencySamplesMs.Count) + "</td>");
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ArtifactBytes, bestHdd));
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ResourcesAfter.PrivateBytes, bestPrivate) + "</tr>");
        }
        builder.AppendLine("</table>");
    }

    private static void AppendCorrectness(
        StringBuilder builder,
        string title,
        QueryResult expected,
        IReadOnlyList<LookupEngineResult> engines,
        Func<LookupEngineResult, long> rows,
        Func<LookupEngineResult, ulong> checksum)
    {
        builder.AppendLine("<h4>" + BenchmarkReportFormat.Escape(title) + "</h4>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Rows</th><th>Checksum</th><th>Status</th></tr>");
        builder.AppendLine("<tr><td>expected</td><td>" + BenchmarkReportFormat.Long(expected.Rows) +
            "</td><td>" + BenchmarkReportFormat.Unsigned(expected.Checksum) + "</td><td class=\"ok\">Baseline</td></tr>");
        foreach (var engine in engines)
        {
            var actualRows = rows(engine);
            var actualChecksum = checksum(engine);
            var ok = actualRows == expected.Rows && actualChecksum == expected.Checksum;
            builder.AppendLine("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) +
                "</td><td>" + BenchmarkReportFormat.Long(actualRows) +
                "</td><td>" + BenchmarkReportFormat.Unsigned(actualChecksum) +
                "</td><td class=\"" + (ok ? "ok" : "warn") + "\">" +
                (ok ? "OK" : "Mismatch") + "</td></tr>");
        }
        builder.AppendLine("</table>");
    }

    private static double RowsPerQuery(LookupEngineResult engine) =>
        engine.BatchQueries == 0 ? 0 : (double)engine.BatchRows / engine.BatchQueries;

    private static long QueriesPerBatch(LookupEngineResult engine) =>
        engine.BatchAvgSamplesMs.Count == 0 ? 0 : engine.BatchQueries / engine.BatchAvgSamplesMs.Count;
}
