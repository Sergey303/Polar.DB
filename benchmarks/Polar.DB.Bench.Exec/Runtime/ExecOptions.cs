namespace Polar.DB.Bench.Exec.Runtime;

public sealed class ExecOptions
{
    public static string UsageText =>
        "Usage: --spec <path> --work <dir> --raw-out <dir> [--engine <key>] " +
        "[--env <class>] [--comparison-set <id>] [--warmup-count <n>] [--measured-count <n>]";

    public string? EngineKey { get; init; }
    public string? SpecPath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? RawResultsDirectory { get; init; }
    public string EnvironmentClass { get; init; } = "local";
    public string? ComparisonSetId { get; init; }
    public int? WarmupCount { get; init; }
    public int? MeasuredCount { get; init; }

    public static ExecOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2)
        {
            map[args[i]] = args[i + 1];
        }

        return new ExecOptions
        {
            EngineKey = map.GetValueOrDefault("--engine"),
            SpecPath = map.GetValueOrDefault("--spec"),
            WorkingDirectory = map.GetValueOrDefault("--work"),
            RawResultsDirectory = map.GetValueOrDefault("--raw-out"),
            EnvironmentClass = map.GetValueOrDefault("--env") ?? "local",
            ComparisonSetId = map.GetValueOrDefault("--comparison-set"),
            WarmupCount = ParseOptionalNonNegativeInt(map, "--warmup-count"),
            MeasuredCount = ParseOptionalPositiveInt(map, "--measured-count")
        };
    }

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(SpecPath) || !File.Exists(SpecPath))
        {
            error = "Missing or invalid --spec path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            error = "Missing --work.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RawResultsDirectory))
        {
            error = "Missing --raw-out.";
            return false;
        }

        if (WarmupCount is < 0)
        {
            error = "--warmup-count must be >= 0.";
            return false;
        }

        if (MeasuredCount is <= 0)
        {
            error = "--measured-count must be >= 1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int? ParseOptionalNonNegativeInt(IReadOnlyDictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var text))
        {
            return null;
        }

        if (int.TryParse(text, out var value) && value >= 0)
        {
            return value;
        }

        return -1;
    }

    private static int? ParseOptionalPositiveInt(IReadOnlyDictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var text))
        {
            return null;
        }

        if (int.TryParse(text, out var value) && value > 0)
        {
            return value;
        }

        return 0;
    }
}
