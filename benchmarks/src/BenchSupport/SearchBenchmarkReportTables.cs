using System.Text;

namespace PolarDbBenchmarks;

internal static class SearchBenchmarkReportTables
{
    public static void AppendPhase(StringBuilder builder, LookupPhaseResult phase)
    {
        builder.AppendLine("<h3>" + BenchmarkReportFormat.Escape(phase.Name) + "</h3>");
        AppendTiming(builder, phase);
        BenchmarkReportTables.AppendCorrectness(builder, phase.Expected, phase.Engines.Select(Convert).ToArray());
    }

    private static void AppendTiming(StringBuilder builder, LookupPhaseResult phase)
    {
        var rows = phase.Engines.Select(e => (Engine: e, Stats: BenchmarkStats.From(e.SamplesMs))).ToArray();
        var bestMedian = rows.Min(row => row.Stats.Median);
        var bestP95 = rows.Min(row => row.Stats.P95);
        var bestTrimmed = rows.Min(row => row.Stats.TrimmedMean);
        var bestHdd = rows.Min(row => row.Engine.ArtifactBytes);

        builder.AppendLine("<table><tr><th>Engine</th><th>Median ms/query</th><th>P95</th><th>Trimmed mean</th><th>Queries</th><th>Rows total</th><th>Rows/query</th><th>Rows/sec</th><th>HDD</th><th>Private RAM</th></tr>");
        foreach (var row in rows)
        {
            var engine = row.Engine;
            builder.Append("<tr><td>" + BenchmarkReportFormat.Escape(engine.Engine) + "</td>");
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.Median, bestMedian, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.P95, bestP95, " ms"));
            builder.Append(BenchmarkReportFormat.Cell(row.Stats.TrimmedMean, bestTrimmed, " ms"));
            builder.Append("<td>" + engine.Queries + "</td><td>" + engine.Rows + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(RowsPerQuery(engine)) + "</td>");
            builder.Append("<td>" + BenchmarkReportFormat.Number(RowsPerSecond(engine)) + "</td>");
            builder.Append(BenchmarkReportFormat.ByteCell(engine.ArtifactBytes, bestHdd));
            builder.Append("<td>" + BenchmarkReportFormat.Bytes(engine.ResourcesAfter.PrivateBytes) + "</td></tr>");
        }

        builder.AppendLine("</table>");
    }

    private static double RowsPerQuery(LookupEngineResult engine) =>
        engine.Queries == 0 ? 0 : (double)engine.Rows / engine.Queries;

    private static double RowsPerSecond(LookupEngineResult engine)
    {
        if (engine.SamplesMs.Count == 0 || engine.Queries == 0) return 0;
        var lookupsPerSample = (double)engine.Queries / engine.SamplesMs.Count;
        var totalMs = engine.SamplesMs.Sum() * lookupsPerSample;
        return totalMs <= 0 ? 0 : engine.Rows / (totalMs / 1000.0);
    }

    private static EngineResult Convert(LookupEngineResult engine) =>
        new(engine.Engine, engine.Status, engine.SamplesMs, engine.Rows, engine.Checksum,
            engine.ArtifactBytes, engine.ResourcesBefore, engine.ResourcesAfter);
}
