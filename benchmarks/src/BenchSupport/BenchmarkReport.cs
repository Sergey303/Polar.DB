using System.Text;

namespace PolarDbBenchmarks;

internal static class BenchmarkReport
{
    public static string Render(ExperimentOptions options, IReadOnlyList<EngineResult> engines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        builder.AppendLine("<title>" + Escape(options.ExperimentId) + "</title>");
        builder.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:32px}table{border-collapse:collapse;margin:12px 0}td,th{border:1px solid #ddd;padding:6px 10px}th{background:#f4f4f4}.ok{color:#177245}.warn{color:#9a6700}</style>");
        builder.AppendLine("</head><body><h1>" + Escape(options.Title) + "</h1>");
        builder.AppendLine("<p><b>Experiment:</b> " + Escape(options.ExperimentId) + "</p>");
        builder.AppendLine("<p><b>Rows:</b> " + options.SetupRows + "; <b>warmup:</b> " +
            options.WarmupOps + "; <b>measured:</b> " + options.MeasuredOps + "</p>");
        AppendTiming(builder, engines);
        AppendCorrectness(builder, engines);
        builder.AppendLine("<h2>Notes</h2>");
        builder.AppendLine("<p>Generated reports and temporary databases are local artifacts.</p>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static void AppendTiming(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        builder.AppendLine("<h2>Timing</h2>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Status</th><th>Median ms</th><th>P95 ms</th><th>Min ms</th><th>Max ms</th><th>Trimmed mean ms</th><th>Rows</th><th>Bytes</th></tr>");
        foreach (var engine in engines)
        {
            var stats = Stats.From(engine.SamplesMs);
            builder.AppendLine("<tr><td>" + Escape(engine.Engine) + "</td><td>" +
                Escape(engine.Status) + "</td><td>" + F(stats.Median) + "</td><td>" +
                F(stats.P95) + "</td><td>" + F(stats.Min) + "</td><td>" +
                F(stats.Max) + "</td><td>" + F(stats.TrimmedMean) + "</td><td>" +
                engine.Rows + "</td><td>" + engine.ArtifactBytes + "</td></tr>");
        }

        builder.AppendLine("</table>");
    }

    private static void AppendCorrectness(StringBuilder builder, IReadOnlyList<EngineResult> engines)
    {
        builder.AppendLine("<h2>Correctness</h2>");
        builder.AppendLine("<table><tr><th>Engine</th><th>Rows</th><th>Checksum</th><th>Status</th></tr>");
        var expected = engines.FirstOrDefault(engine => engine.Status == "Measured")?.Checksum;
        foreach (var engine in engines)
        {
            var ok = expected == engine.Checksum || engine.Status != "Measured";
            builder.AppendLine("<tr><td>" + Escape(engine.Engine) + "</td><td>" +
                engine.Rows + "</td><td>" + engine.Checksum + "</td><td class=\"" +
                (ok ? "ok" : "warn") + "\">" + (ok ? "OK" : "Mismatch") + "</td></tr>");
        }

        builder.AppendLine("</table>");
    }

    private static string F(double value) => double.IsNaN(value) ? "N/A" : value.ToString("0.######");

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private sealed record Stats(double Min, double Median, double P95, double Max, double TrimmedMean)
    {
        public static Stats From(IReadOnlyList<double> source)
        {
            if (source.Count == 0) return new Stats(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
            var values = source.OrderBy(value => value).ToArray();
            var skip = values.Length >= 10 ? values.Length / 10 : 0;
            var trimmed = values.Skip(skip).Take(values.Length - 2 * skip).ToArray();
            return new Stats(values[0], Quantile(values, 0.5), Quantile(values, 0.95), values[^1], trimmed.Average());
        }

        private static double Quantile(double[] values, double q)
        {
            var index = (int)Math.Ceiling(q * values.Length) - 1;
            return values[Math.Clamp(index, 0, values.Length - 1)];
        }
    }
}
