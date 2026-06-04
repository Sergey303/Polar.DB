namespace PolarDbBenchmarks;

internal sealed record BenchmarkStats(
    double Min,
    double Median,
    double P95,
    double Max,
    double TrimmedMean)
{
    public static BenchmarkStats From(IReadOnlyList<double> source)
    {
        if (source.Count == 0)
            return new BenchmarkStats(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

        var values = source.OrderBy(value => value).ToArray();
        var skip = values.Length >= 10 ? values.Length / 10 : 0;
        var trimmed = values.Skip(skip).Take(values.Length - 2 * skip).ToArray();
        return new BenchmarkStats(
            values[0],
            Quantile(values, 0.5),
            Quantile(values, 0.95),
            values[^1],
            trimmed.Average());
    }

    private static double Quantile(double[] values, double q)
    {
        var index = (int)Math.Ceiling(q * values.Length) - 1;
        return values[Math.Clamp(index, 0, values.Length - 1)];
    }
}
