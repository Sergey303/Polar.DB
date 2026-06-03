namespace PolarDbBenchmarks;

internal static class BenchmarkArgs
{
    public static IReadOnlyList<int> Rows(string[] args, params int[] defaults)
    {
        var raw = Value(args, "rows");
        if (string.IsNullOrWhiteSpace(raw)) return defaults;

        var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.Parse(value))
            .Where(value => value > 0)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        return values.Length == 0 ? defaults : values;
    }

    public static int Int(string[] args, string name, int defaultValue)
    {
        var raw = Value(args, name);
        return int.TryParse(raw, out var value) && value >= 0 ? value : defaultValue;
    }

    public static long MemoryLimitBytes(string[] args, int defaultGb)
    {
        var raw = Value(args, "ram-gb");
        var gb = int.TryParse(raw, out var value) && value > 0 ? value : defaultGb;
        return gb * 1024L * 1024L * 1024L;
    }

    private static string? Value(string[] args, string name)
    {
        var prefix = "--" + name + "=";
        var exact = "--" + name;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return args[i][prefix.Length..];
            if (string.Equals(args[i], exact, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }
}
