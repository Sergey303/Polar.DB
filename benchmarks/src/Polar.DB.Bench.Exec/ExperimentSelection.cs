using System;
using System.IO;
using System.Linq;

namespace Polar.DB.Bench.Exec;

public static class ExperimentSelection
{
    public static bool IsInteractiveSession()
    {
        return Environment.UserInteractive
               && !Console.IsInputRedirected
               && !Console.IsOutputRedirected
               && !Console.IsErrorRedirected;
    }

    public static bool TrySelectExperiment(
        string benchmarksRoot,
        out string? experimentPath,
        out string error)
    {
        experimentPath = null;

        var experimentsRoot = Path.Combine(benchmarksRoot, "experiments");
        if (!Directory.Exists(experimentsRoot))
        {
            error = $"Experiments root not found: '{experimentsRoot}'.";
            return false;
        }

        var candidates = new DirectoryInfo(experimentsRoot)
            .EnumerateDirectories()
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Take(5)
            .ToArray();

        if (candidates.Length == 0)
        {
            error = $"No experiment folders found in '{experimentsRoot}'.";
            return false;
        }

        Console.WriteLine("Select experiment to run:");
        for (var i = 0; i < candidates.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {candidates[i].Name}");
        }

        Console.Write($"Enter number 1..{candidates.Length} (q to cancel): ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Selection cancelled: empty input.";
            return false;
        }

        if (input.Equals("q", StringComparison.OrdinalIgnoreCase)
            || input.Equals("quit", StringComparison.OrdinalIgnoreCase)
            || input.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            error = "Selection cancelled.";
            return false;
        }

        if (!int.TryParse(input, out var selectedIndex)
            || selectedIndex < 1
            || selectedIndex > candidates.Length)
        {
            error = "Selection cancelled: expected a number from the list.";
            return false;
        }

        experimentPath = candidates[selectedIndex - 1].FullName;
        error = string.Empty;
        return true;
    }
}
