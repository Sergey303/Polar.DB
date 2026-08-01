using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal static class BenchmarkEnvironment
{
    public static BenchmarkEnvironmentManifest Capture(
        string runId,
        string experimentId,
        string processRole,
        BenchmarkEngine? engine)
    {
        var root = BenchmarkPaths.RepoRoot;
        var drive = TryDrive(root);
        var commit = Git(root, "rev-parse", "HEAD");
        var status = Git(root, "status", "--porcelain", "--untracked-files=no");
        var assembly = typeof(BenchmarkEnvironment).Assembly;
        var buildConfiguration = AssemblyMetadata(assembly, "BenchmarkBuildConfiguration") ?? "unknown";
        var debugAttribute = assembly.GetCustomAttribute<DebuggableAttribute>();
        var optimizationsDisabled = debugAttribute?.IsJITOptimizerDisabled ?? false;
        var isDebugBuild = buildConfiguration.Equals("Debug", StringComparison.OrdinalIgnoreCase)
            || optimizationsDisabled;
        var publicationReady = buildConfiguration.Equals("Release", StringComparison.OrdinalIgnoreCase)
            && !optimizationsDisabled;

        return new BenchmarkEnvironmentManifest(
            RunId: runId,
            ExperimentId: experimentId,
            ProcessRole: processRole,
            Engine: engine == null ? null : BenchmarkPaths.EngineToken(engine.Value),
            ProcessId: System.Environment.ProcessId,
            CapturedUtc: DateTimeOffset.UtcNow,
            CommitSha: commit ?? "unknown",
            GitDirty: status != null && !string.IsNullOrWhiteSpace(status),
            GitStatusKnown: status != null,
            RuntimeVersion: System.Environment.Version.ToString(),
            FrameworkDescription: RuntimeInformation.FrameworkDescription,
            OsDescription: RuntimeInformation.OSDescription,
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount: System.Environment.ProcessorCount,
            ServerGc: GCSettings.IsServerGC,
            BuildConfiguration: buildConfiguration,
            IsDebugBuild: isDebugBuild,
            OptimizationsDisabled: optimizationsDisabled,
            PublicationReady: publicationReady,
            TieredCompilationSetting: RuntimeSetting("DOTNET_TieredCompilation", "COMPlus_TieredCompilation"),
            TieredPgoSetting: RuntimeSetting("DOTNET_TieredPGO", "COMPlus_TieredPGO"),
            ReadyToRunSetting: RuntimeSetting("DOTNET_ReadyToRun", "COMPlus_ReadyToRun"),
            CpuDescription: CpuDescription(),
            PolarDbAssemblyVersion: typeof(USequence).Assembly.GetName().Version?.ToString() ?? "unknown",
            SqliteAssemblyVersion: typeof(SqliteConnection).Assembly.GetName().Version?.ToString() ?? "unknown",
            CurrentDirectory: System.Environment.CurrentDirectory,
            CommandLine: System.Environment.CommandLine,
            TimeZone: TimeZoneInfo.Local.Id,
            Culture: CultureInfo.CurrentCulture.Name,
            DriveTotalBytes: DriveValue(drive, total: true),
            DriveAvailableBytes: DriveValue(drive, total: false));
    }

    public static string NewRunId(string experimentId)
    {
        var series = System.Environment.GetEnvironmentVariable("POLAR_BENCH_RUN_ID");
        if (!string.IsNullOrWhiteSpace(series)) return Sanitize(series);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
        var shortCommit = Git(BenchmarkPaths.RepoRoot, "rev-parse", "--short=8", "HEAD") ?? "nogit";
        return Sanitize(stamp + "-" + shortCommit + "-" + experimentId);
    }

    private static string? AssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value;

    private static string RuntimeSetting(params string[] names)
    {
        foreach (var name in names)
        {
            var value = System.Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) return name + "=" + value;
        }

        return "runtime-default";
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch).ToArray());
    }

    private static string CpuDescription()
    {
        var value = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        if (!string.IsNullOrWhiteSpace(value)) return value;

        try
        {
            const string cpuInfo = "/proc/cpuinfo";
            if (File.Exists(cpuInfo))
            {
                var model = File.ReadLines(cpuInfo)
                    .FirstOrDefault(line => line.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                var separator = model?.IndexOf(':') ?? -1;
                if (separator >= 0) return model![(separator + 1)..].Trim();
            }
        }
        catch
        {
            // Fall back to a generic architecture description.
        }

        return System.Environment.GetEnvironmentVariable("HOSTTYPE")
            ?? RuntimeInformation.ProcessArchitecture.ToString();
    }

    private static long? DriveValue(DriveInfo? drive, bool total)
    {
        try
        {
            if (drive == null || !drive.IsReady) return null;
            return total ? drive.TotalSize : drive.AvailableFreeSpace;
        }
        catch
        {
            return null;
        }
    }

    private static DriveInfo? TryDrive(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return string.IsNullOrWhiteSpace(root) ? null : new DriveInfo(root);
        }
        catch
        {
            return null;
        }
    }

    private static string? Git(string workingDirectory, params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            if (process == null) return null;
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? stdout.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
