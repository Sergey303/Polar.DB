namespace PolarDbBenchmarks;

internal sealed class BenchmarkProgress
{
    private readonly string _prefix;
    private readonly int _total;
    private int _nextPercent;

    public BenchmarkProgress(string prefix, int total)
    {
        _prefix = prefix;
        _total = Math.Max(1, total);
        _nextPercent = 0;
    }

    public void Step(int completed)
    {
        var percent = Math.Clamp(completed * 100 / _total, 0, 100);
        if (percent < _nextPercent && completed < _total) return;

        Console.WriteLine(_prefix + ": " + percent + "% (" + completed + "/" + _total + ")");
        _nextPercent = Math.Min(100, percent + 10);
    }

    public static void Stage(string message) => Console.WriteLine("[bench] " + message);
}
