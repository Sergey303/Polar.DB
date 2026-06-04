using System.Text;

namespace PolarDbBenchmarks;

internal static class LookupReport
{
    public static string Render(LookupOptions o, IReadOnlyList<EngineResult> engines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>" + Escape(o.ExperimentId) + "</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:32px}table{border-collapse:collapse;margin:12px 0}td,th{border:1px solid #ddd;padding:6px 10px}th{background:#f4f4f4}.ok{color:#177245}.warn{color:#9a6700}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>" + Escape(o.Title) + "</h1>");
        sb.AppendLine("<p><b>Experiment:</b> " + Escape(o.ExperimentId) + "</p>");
        sb.AppendLine("<p><b>Measured operation:</b> lookup only. Data generation, load, build and reopen are setup.</p>");
        sb.AppendLine("<p><b>Rows:</b> " + o.SetupRows + "; <b>warmup:</b> " + o.WarmupOps + "; <b>measured:</b> " + o.MeasuredOps + "</p>");
        AppendTiming(sb, engines);
        AppendCorrectness(sb, engines);
        sb.AppendLine("<h2>Notes</h2>");
        sb.AppendLine("<p>Both engines materialize record values and compute the same checksum shape after lookup.</p>");
        sb.AppendLine("<p class=\"warn\">This is still a direct benchmark implementation, not the old benchmark runner/charts pipeline.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendTiming(StringBuilder sb, IReadOnlyList<EngineResult> engines)
    {
        sb.AppendLine("<h2>Timing</h2>");
        sb.AppendLine("<table><tr><th>Engine</th><th>Status</th><th>Median ms</th><th>P95 ms</th><th>Min ms</th><th>Max ms</th><th>Trimmed mean ms</th><th>Total rows</th><th>Artifact bytes</th></tr>");
        foreach (var e in engines)
        {
            var s = Stats.From(e.SamplesMs);
            sb.AppendLine("<tr><td>" + Escape(e.Engine) + "</td><td>" + Escape(e.Status) +
                "</td><td>" + F(s.Median) + "</td><td>" + F(s.P95) +
                "</td><td>" + F(s.Min) + "</td><td>" + F(s.Max) +
                "</td><td>" + F(s.TrimmedMean) + "</td><td>" + e.Rows +
                "</td><td>" + e.ArtifactBytes + "</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    private static void AppendCorrectness(StringBuilder sb, IReadOnlyList<EngineResult> engines)
    {
        sb.AppendLine("<h2>Correctness</h2>");
        sb.AppendLine("<table><tr><th>Engine</th><th>Rows</th><th>Checksum</th><th>Status</th></tr>");
        var expected = engines.FirstOrDefault()?.Checksum;
        foreach (var e in engines)
        {
            var ok = expected == e.Checksum;
            sb.AppendLine("<tr><td>" + Escape(e.Engine) + "</td><td>" + e.Rows +
                "</td><td>" + e.Checksum + "</td><td class=\"" + (ok ? "ok" : "warn") +
                "\">" + (ok ? "OK" : "Mismatch") + "</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    private static string F(double value) => double.IsNaN(value) ? "N/A" : value.ToString("0.######");

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private sealed record Stats(double Min, double Median, double P95, double Max, double TrimmedMean)
    {
        public static Stats From(IReadOnlyList<double> source)
        {
            if (source.Count == 0) return new Stats(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
            var xs = source.OrderBy(x => x).ToArray();
            var trim = xs.Length >= 10 ? xs.Skip(xs.Length / 10).Take(xs.Length - 2 * (xs.Length / 10)).ToArray() : xs;
            return new Stats(xs[0], Quantile(xs, 0.5), Quantile(xs, 0.95), xs[^1], trim.Average());
        }

        private static double Quantile(double[] xs, double q)
        {
            var index = (int)Math.Ceiling(q * xs.Length) - 1;
            return xs[Math.Clamp(index, 0, xs.Length - 1)];
        }
    }
}
