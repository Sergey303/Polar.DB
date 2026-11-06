using System.Diagnostics;
using System.Text.Json;

namespace PolarDbBenchmarks;

internal static partial class PreparedLookupRunner
{
    private sealed record MetricSnapshot(long StopwatchTicks, long ManagedBytes,
        long ProcessWorkingSetBytes, long ProcessPrivateBytes,
        long CgroupCurrentBytes, long CgroupLifetimePeakBytes,
        Dictionary<string, long> ProcessIo,
        Dictionary<string, long> MemoryCgroup, Dictionary<string, long> MemoryEvents);

    private static MetricSnapshot Capture()
    {
        using var p = Process.GetCurrentProcess();
        return new MetricSnapshot(Stopwatch.GetTimestamp(), GC.GetTotalMemory(false),
            p.WorkingSet64, p.PrivateMemorySize64,
            NumericFile("/sys/fs/cgroup/memory.current"),
            NumericFile("/sys/fs/cgroup/memory.peak"),
            KeyValueFile("/proc/self/io"), KeyValueFile("/sys/fs/cgroup/memory.stat"),
            KeyValueFile("/sys/fs/cgroup/memory.events"));
    }

    private static long NumericFile(string path) =>
        File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out var value)
            ? value : -1;

    private static Dictionary<string, long> KeyValueFile(string path)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Replace(':', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && long.TryParse(parts[1], out var value)) result[parts[0]] = value;
        }
        return result;
    }

    private static void CheckDiskReserve(string target)
    {
        // The Docker volume can have a different capacity from the container root.
        var full = Path.GetFullPath(target);
        var drive = DriveInfo.GetDrives()
            .Where(d => IsWithin(full, d.RootDirectory.FullName))
            .OrderByDescending(d => d.RootDirectory.FullName.Length)
            .FirstOrDefault() ?? throw new IOException("Unable to identify target filesystem.");
        if (drive.AvailableFreeSpace < 24 * GiB)
            throw new IOException("Less than 24 GiB free on the prepared-data volume.");
    }

    private static void WriteNewJson<T>(string path, T payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(fs, payload, Json);
    }

    private static string RequiredDirectory(string[] args, string name)
    {
        var raw = Value(args, name);
        if (string.IsNullOrWhiteSpace(raw) || !Path.IsPathFullyQualified(raw))
            throw new ArgumentException("--" + name + " requires an absolute directory path.");
        return Path.GetFullPath(raw);
    }

    private static string? Value(string[] args, string name)
    {
        var prefix = "--" + name + "=";
        for (var i = 0; i < args.Length; i++)
            if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return args[i][prefix.Length..];
        return null;
    }

    private static string CurrentCommit()
    {
        using var process = Process.Start(new ProcessStartInfo("git") {
            WorkingDirectory = BenchmarkPaths.RepoRoot, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "rev-parse", "HEAD" }
        }) ?? throw new IOException("Unable to start git.");
        var result = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0 || result.Length != 40) throw new IOException("Unable to identify source SHA.");
        return result;
    }

    private static bool IsWithin(string path, string baseDir) =>
        path.Equals(baseDir, StringComparison.Ordinal) ||
        path.StartsWith(baseDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
}
