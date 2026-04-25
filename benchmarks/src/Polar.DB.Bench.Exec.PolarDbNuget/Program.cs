using System.Text.Json;
using Polar.DB.Bench.Exec.PolarDbNuget.Cli;
using Polar.DB.Bench.Exec.PolarDbNuget.Contracts;
using Polar.DB.Bench.Exec.PolarDbNuget.Execution;

namespace Polar.DB.Bench.Exec.PolarDbNuget;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static int Main(string[] args)
    {
        RunnerOptions options;

        try
        {
            options = CommandLineParser.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CommandLineParser.HelpText);
            return 2;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(CommandLineParser.HelpText);
            return 0;
        }

        var startedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            var runner = new PolarDbNugetRunner();
            RawRunResult result = runner.Execute(options, startedAtUtc);
            WriteResult(options.OutputPath, result);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            var result = RawRunResult.Failed(
                runId: RunIdFactory.Create(options.EngineKey, options.Mode.ToString().ToLowerInvariant(), startedAtUtc),
                engineKey: options.EngineKey,
                mode: options.Mode.ToString().ToLowerInvariant(),
                startedAtUtc: startedAtUtc,
                endedAtUtc: DateTimeOffset.UtcNow,
                error: ErrorInfo.FromException(ex));

            WriteResult(options.OutputPath, result);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void WriteResult(string outputPath, RawRunResult result)
    {
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        File.WriteAllText(fullPath, JsonSerializer.Serialize(result, JsonOptions));
        Console.WriteLine(fullPath);
    }
}
