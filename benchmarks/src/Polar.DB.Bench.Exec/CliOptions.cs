using System;
using System.Text;

namespace Polar.DB.Bench.Exec;

public sealed class CliOptions
{
    private CliOptions()
    {
    }

    public string? ExperimentInput { get; private set; }

    public bool SmokeCleanup { get; private set; }

    public bool ShowHelp { get; private set; }

    public static string UsageText
    {
        get
        {
            var nl = Environment.NewLine;
            var builder = new StringBuilder();
            builder.Append("Usage:").Append(nl);
            builder.Append("  Polar.DB.Bench.Exec --exp <experiment-folder-or-name>").Append(nl).Append(nl);
            builder.Append("Examples:").Append(nl);
            builder.Append("  Polar.DB.Bench.Exec --exp persons-full-adapter-coverage-version-matrix").Append(nl);
            builder.Append("  Polar.DB.Bench.Exec --exp .\\benchmarks\\experiments\\persons-full-adapter-coverage-version-matrix").Append(nl).Append(nl);
            builder.Append("Dev mode:").Append(nl);
            builder.Append("  Polar.DB.Bench.Exec --smoke-cleanup");
            return builder.ToString();
        }
    }

    public static bool TryParse(string[] args, out CliOptions options, out string error)
    {
        options = new CliOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            switch (argument)
            {
                case "--exp":
                    if (!string.IsNullOrWhiteSpace(options.ExperimentInput))
                    {
                        error = "--exp is specified more than once.";
                        return false;
                    }

                    if (index + 1 >= args.Length)
                    {
                        error = "Missing value for --exp.";
                        return false;
                    }

                    var value = args[++index];
                    if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "Value for --exp must be a non-empty experiment folder path or name.";
                        return false;
                    }

                    options.ExperimentInput = value;
                    break;

                case "--smoke-cleanup":
                    if (options.SmokeCleanup)
                    {
                        error = "--smoke-cleanup is specified more than once.";
                        return false;
                    }

                    options.SmokeCleanup = true;
                    break;

                case "-h":
                case "--help":
                case "/?":
                    options.ShowHelp = true;
                    break;

                case "--all":
                    error = "Option --all is not supported. Use --exp or interactive single-experiment selection.";
                    return false;

                default:
                    error = $"Unknown argument: '{argument}'.";
                    return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(options.ExperimentInput) && options.SmokeCleanup)
        {
            error = "Use either --exp or --smoke-cleanup, not both.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
