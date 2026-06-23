using System.Globalization;

namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Управляет только папками эпох хранения.
/// Не знает, какие файлы лежат внутри эпохи.
/// </summary>
public sealed class EpochFolderManager
{
    public const string BuildingSuffix = ".building";
    public const string ReadySuffix = ".ready";
    public const string ReadyMarkerFileName = "_epoch.ready";

    private const string NameFormat = "yyyy-MM-ddTHH-mm-ss'Z'";
    private readonly string _rootPath;

    public EpochFolderManager(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path is required.", nameof(rootPath));

        _rootPath = Path.GetFullPath(rootPath);
    }

    /// <summary>
    /// Последняя завершённая эпоха. Папки без marker-файла игнорируются.
    /// </summary>
    public string? GetLatestReady()
    {
        return ListReady().LastOrDefault();
    }

    /// <summary>
    /// Все завершённые эпохи по возрастанию времени.
    /// </summary>
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

    /// <summary>
    /// Создаёт рабочую папку эпохи. Такая папка ещё не считается готовой.
    /// </summary>
    public string CreateBuilding(DateTimeOffset? utcTime = null)
    {
        Directory.CreateDirectory(_rootPath);

        var utc = (utcTime ?? DateTimeOffset.UtcNow).ToUniversalTime();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidateUtc = utc.AddSeconds(attempt);
            var stamp = FormatName(candidateUtc);
            var epochPath = Path.Combine(_rootPath, stamp);

            if (Directory.Exists(epochPath)
                || Directory.Exists(epochPath + BuildingSuffix)
                || Directory.Exists(epochPath + ReadySuffix))
                continue;

            Directory.CreateDirectory(epochPath);
            return Make(epochPath, candidateUtc, EpochState.Building).Path;
        }

        throw new IOException("Could not create a unique epoch folder.");
    }

    /// <summary>
    /// Последним действием создаёт marker-файл готовности внутри папки эпохи.
    /// Папка не переименовывается.
    /// </summary>
    public string MarkReady(string path)
    {
        var epoch = TryRead(path);
        if (epoch == null)
            throw new IOException($"Could not read epoch folder: {path}");

        var sourcePath = Path.GetFullPath(epoch.Path);
        if (!IsUnderRoot(sourcePath))
            throw new ArgumentException("Epoch folder is outside root path.", nameof(path));
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException(sourcePath);

        var readyPath = Path.Combine(sourcePath, ReadyMarkerFileName);
        if (File.Exists(readyPath))
            return sourcePath;

        using (var stream = new FileStream(readyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Flush(true);
        }

        return Make(sourcePath, epoch.UtcTime, EpochState.Ready).Path;
    }

    private static EpochFolderData? TryRead(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var stamp = StripKnownSuffix(name);
        if (!DateTimeOffset.TryParseExact(
                stamp,
                NameFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utc))
            return null;

        var state = File.Exists(Path.Combine(path, ReadyMarkerFileName))
            ? EpochState.Ready
            : EpochState.Building;
        return Make(path, utc, state);
    }

    private bool IsUnderRoot(string path)
    {
        var relative = Path.GetRelativePath(_rootPath, path);
        return relative != "."
            && !Path.IsPathRooted(relative)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && relative != "..";
    }

    private static string StripKnownSuffix(string name)
    {
        if (name.EndsWith(BuildingSuffix, StringComparison.Ordinal))
            return name[..^BuildingSuffix.Length];
        if (name.EndsWith(ReadySuffix, StringComparison.Ordinal))
            return name[..^ReadySuffix.Length];
        return name;
    }

    private static EpochFolderData Make(string path, DateTimeOffset utc, EpochState state)
    {
        return new EpochFolderData(Path.GetFileName(path), path, utc, state);
    }

    private static string FormatName(DateTimeOffset utc)
    {
        return utc.ToUniversalTime().ToString(NameFormat, CultureInfo.InvariantCulture);
    }
}
