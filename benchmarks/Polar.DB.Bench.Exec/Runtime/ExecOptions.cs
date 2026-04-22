namespace Polar.DB.Bench.Exec.Runtime;

public sealed class ExecOptions
{
    public static string UsageText => "Usage: --engine <key> --spec <path> --work <dir> --raw-out <dir>";

    public string? EngineKey { get; init; }
    public string? SpecPath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? RawResultsDirectory { get; init; }
    public string EnvironmentClass { get; init; } = "local";

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
            EnvironmentClass = map.GetValueOrDefault("--env") ?? "local"
        };
    }

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(EngineKey))
        {
            error = "Missing --engine.";
            return false;
        }

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

        error = string.Empty;
        return true;
    }
}
