using System.Diagnostics;

internal static class Check
{
    internal static void That(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed: " + label);
    }

    internal static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
    }

    internal static void Rate(string label, long count, TimeSpan elapsed)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 0.000001);
        Console.WriteLine($"{label} ms={elapsed.TotalMilliseconds:F1} rate_million_per_s={count / seconds / 1_000_000:F3}");
    }
}
