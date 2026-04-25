using System.Text.RegularExpressions;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Cli;

internal static class CommandLineParser
{
    private static readonly Regex EngineKeyVersionRegex = new(
        @"polar-db-(?<version>[0-9]+(?:\.[0-9]+){1,3}(?:[-a-zA-Z0-9.]+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StandaloneSemVerRegex = new(
        @"(?<![0-9A-Za-z.])(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:[-a-zA-Z0-9.]+)?)(?![0-9A-Za-z.])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public const string HelpText = """
Polar.DB.Bench.Exec.PolarDbNuget

Usage:
  --mode probe|run
  [--engine-key <key>]
  (--package-version <version> [--package-id Polar.DB] [--tfm netstandard2.0] [--nuget-cache <path>] | --polar-dll <path>)
  [--experiment <path>]
  [--work-dir <path>]
  --output <path>
  [--keep-work-dir]

Notes:
  --package-version may be omitted when --engine-key, --work-dir, --output,
  or another argument contains a value like polar-db-2.1.0.

Examples:
  dotnet run --project Polar.DB.Bench.Exec.PolarDbNuget -- --mode probe --engine-key polar-db-2.1.1 --package-version 2.1.1 --output results/raw/probe.json
  dotnet run --project Polar.DB.Bench.Exec.PolarDbNuget -- --mode run --engine-key polar-db-2.1.0 --package-version 2.1.0 --experiment experiments/polar-db-nuget-smoke.experiment.json --work-dir .work/polar-db-2.1.0 --output results/raw/polar-db-2.1.0.raw.json
""";

    public static RunnerOptions Parse(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            return new RunnerOptions { ShowHelp = true };
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected positional argument: {arg}");
            }

            if (arg is "--keep-work-dir")
            {
                flags.Add(arg);
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for option: {arg}");
            }

            values[arg] = args[++i];
        }

        var mode = ParseMode(Get(values, "--mode", "run"));
        var explicitEngineKey = GetNullable(values, "--engine-key");
        var polarDll = GetNullable(values, "--polar-dll");
        var packageVersion = GetNullable(values, "--package-version")
            ?? TryInferPackageVersionFromEngineKey(explicitEngineKey)
            ?? TryInferPackageVersionFromKnownArguments(values)
            ?? TryInferPackageVersionFromAnyArgument(args);

        var engineKey = explicitEngineKey;
        if (string.IsNullOrWhiteSpace(engineKey))
        {
            engineKey = !string.IsNullOrWhiteSpace(packageVersion)
                ? "polar-db-" + packageVersion
                : "polar-db-nuget";
        }

        var output = Get(values, "--output", Path.Combine("benchmarks", "results", "raw", engineKey + ".raw.json"));

        if (string.IsNullOrWhiteSpace(packageVersion) && string.IsNullOrWhiteSpace(polarDll))
        {
            throw new ArgumentException(
                "Specify either --package-version or --polar-dll. " +
                "For convenience, --package-version can be omitted when --engine-key, --work-dir, --output, " +
                "or another argument contains a value like polar-db-2.1.0.");
        }

        if (!string.IsNullOrWhiteSpace(packageVersion) && !string.IsNullOrWhiteSpace(polarDll))
        {
            throw new ArgumentException("Specify only one of --package-version or --polar-dll.");
        }

        if (mode == RunnerMode.Run && string.IsNullOrWhiteSpace(GetNullable(values, "--experiment")))
        {
            throw new ArgumentException("Run mode requires --experiment.");
        }

        return new RunnerOptions
        {
            Mode = mode,
            EngineKey = engineKey,
            PackageVersion = packageVersion,
            PackageId = Get(values, "--package-id", "Polar.DB"),
            TargetFrameworkMoniker = Get(values, "--tfm", "netstandard2.0"),
            NugetCachePath = GetNullable(values, "--nuget-cache"),
            PolarDllPath = polarDll,
            ExperimentPath = GetNullable(values, "--experiment"),
            WorkDirectory = Get(values, "--work-dir", Path.Combine("benchmarks", ".work", engineKey)),
            OutputPath = output,
            KeepWorkDirectory = flags.Contains("--keep-work-dir")
        };
    }

    private static string? TryInferPackageVersionFromEngineKey(string? engineKey)
    {
        if (string.IsNullOrWhiteSpace(engineKey))
        {
            return null;
        }

        var match = EngineKeyVersionRegex.Match(engineKey);
        if (!match.Success)
        {
            return null;
        }

        var version = match.Groups["version"].Value.Trim();
        return IsUsefulVersion(version) ? version : null;
    }

    private static string? TryInferPackageVersionFromKnownArguments(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var key in new[] { "--work-dir", "--output", "--experiment" })
        {
            var value = GetNullable(values, key);
            var version = TryInferPackageVersionFromText(value);
            if (version is not null)
            {
                return version;
            }
        }

        return null;
    }

    private static string? TryInferPackageVersionFromAnyArgument(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            var version = TryInferPackageVersionFromText(arg);
            if (version is not null)
            {
                return version;
            }
        }

        return null;
    }

    private static string? TryInferPackageVersionFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var engineKeyMatch = EngineKeyVersionRegex.Match(text);
        if (engineKeyMatch.Success)
        {
            var version = engineKeyMatch.Groups["version"].Value.Trim();
            if (IsUsefulVersion(version))
            {
                return version;
            }
        }

        var semVerMatch = StandaloneSemVerRegex.Match(text);
        if (semVerMatch.Success)
        {
            var version = semVerMatch.Groups["version"].Value.Trim();
            if (IsUsefulVersion(version))
            {
                return version;
            }
        }

        return null;
    }

    private static bool IsUsefulVersion(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.Equals("current", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals("nuget", StringComparison.OrdinalIgnoreCase) &&
               value.Any(char.IsDigit);
    }

    private static RunnerMode ParseMode(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "probe" => RunnerMode.Probe,
            "run" => RunnerMode.Run,
            _ => throw new ArgumentException("--mode must be probe or run.")
        };
    }

    private static string Get(IReadOnlyDictionary<string, string?> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value!
            : defaultValue;
    }

    private static string? GetNullable(IReadOnlyDictionary<string, string?> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
