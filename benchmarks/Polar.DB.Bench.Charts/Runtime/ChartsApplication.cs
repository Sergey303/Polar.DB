using System.Text;
using System.Text.Json;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Charts.Runtime;

public static class ChartsApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = ChartsOptions.Parse(args);
        if (!options.IsValid(out var validationError))
        {
            Console.Error.WriteLine(validationError);
            Console.Error.WriteLine(ChartsOptions.UsageText);
            return 2;
        }

        var files = Directory.GetFiles(options.AnalyzedResultsDirectory!, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<AnalyzedResult>();
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var value = await JsonSerializer.DeserializeAsync<AnalyzedResult>(stream, JsonDefaults.Default);
            if (value is not null)
            {
                results.Add(value);
            }
        }

        Directory.CreateDirectory(options.ReportsDirectory!);
        await File.WriteAllTextAsync(Path.Combine(options.ReportsDirectory!, "summary.md"), BuildMarkdown(results));
        await File.WriteAllTextAsync(Path.Combine(options.ReportsDirectory!, "summary.csv"), BuildCsv(results));

        Console.WriteLine($"Reports written to: {options.ReportsDirectory}");
        return 0;
    }

    private static string BuildMarkdown(IReadOnlyList<AnalyzedResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Stage-1 Benchmark Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("| RunId | Status | Policy | Baseline |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var result in results)
        {
            sb.AppendLine($"| {Escape(result.RunId)} | {Escape(result.OverallStatus)} | {Escape(result.PolicyId ?? string.Empty)} | {Escape(result.BaselineId ?? string.Empty)} |");
        }

        sb.AppendLine();
        sb.AppendLine("Stage 1 emits markdown and CSV only. Real charts remain a later milestone.");
        return sb.ToString();
    }

    private static string BuildCsv(IReadOnlyList<AnalyzedResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RunId,OverallStatus,PolicyId,BaselineId");
        foreach (var result in results)
        {
            sb.AppendLine($"{Csv(result.RunId)},{Csv(result.OverallStatus)},{Csv(result.PolicyId ?? string.Empty)},{Csv(result.BaselineId ?? string.Empty)}");
        }

        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|");

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
