using System.Globalization;

namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Owns only epoch folders and the ready marker protocol.
/// It does not know which storage files are inside an epoch.
/// </summary>
public sealed class EpochFolderManager
{
    public const string ReadyMarkerFileName = "_epoch.ready";

    private const string NameFormat = "yyyy-MM-ddTHH-mm-ss'Z'";
    private readonly string _rootPath;

    public EpochFolderManager(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path is required.", nameof(rootPath));

        _rootPath = Path.GetFullPath(rootPath);
    }

    public string? GetLatestReady() => ListReady().LastOrDefault();

    public IReadOnlyList<string> ListReady()
    {
        Directory.CreateDirectory(_rootPath);

        return Directory.EnumerateDirectories(_rootPath)
            .Select(TryRead)
            .Where(epoch => epoch is { State: EpochState.Ready })
            .Select(epoch => epoch!)
            .OrderBy(epoch => epoch.UtcTime)
            .ThenBy(epoch => epoch.Name, StringComparer.Ordinal)
            .Select(epoch => epoch.Path)
            .ToArray();
    }

    public string CreateBuilding(DateTimeOffset? utcTime = null)
    {
        Directory.CreateDirectory(_rootPath);
        var utc = (utcTime ?? DateTimeOffset.UtcNow).ToUniversalTime();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidateUtc = utc.AddSeconds(attempt);
            var epochPath = Path.Combine(_rootPath, FormatName(candidateUtc));

            if (Directory.Exists(epochPath)) continue;
            Directory.CreateDirectory(epochPath);
            return epochPath;
        }

        throw new IOException("Could not create a unique epoch folder.");
    }

    public string MarkReady(string path)
    {
        var sourcePath = Path.GetFullPath(path);
        if (!IsUnderRoot(sourcePath))
            throw new ArgumentException("Epoch folder is outside root path.", nameof(path));
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException(sourcePath);

        var readyPath = Path.Combine(sourcePath, ReadyMarkerFileName);
        if (!File.Exists(readyPath))
        {
            using var stream = new FileStream(
                readyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Flush(true);
        }

        return sourcePath;
    }

    private EpochFolderData? TryRead(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (!DateTimeOffset.TryParseExact(
                name,
                NameFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utc))
            return null;

        var state = File.Exists(Path.Combine(path, ReadyMarkerFileName))
            ? EpochState.Ready
            : EpochState.Building;
        return new EpochFolderData(name, path, utc, state);
    }

    private bool IsUnderRoot(string path)
    {
        var relative = Path.GetRelativePath(_rootPath, path);
        return relative != "."
            && !Path.IsPathRooted(relative)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && relative != "..";
    }

    private static string FormatName(DateTimeOffset utc) =>
        utc.ToUniversalTime().ToString(NameFormat, CultureInfo.InvariantCulture);
}
