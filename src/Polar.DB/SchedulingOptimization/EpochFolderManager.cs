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

    private const string NameFormat = "yyyy-MM-ddTHH-mm-ss'Z'";
    private readonly string _rootPath;

    public EpochFolderManager(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path is required.", nameof(rootPath));

        _rootPath = Path.GetFullPath(rootPath);
    }


    /// <summary>
    /// Последняя завершённая эпоха. Незавершённые .building папки игнорируются.
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
            .Select(path => TryRead(path, ReadySuffix))
            .Where(epoch => epoch is not null)
            .Select(epoch => epoch!)
            .OrderBy(epoch => epoch.UtcTime)
            .ThenBy(epoch => epoch.Name, StringComparer.Ordinal)
            .Select(epoch => epoch.Path)
            .ToArray();
    }

    /// <summary>
    /// Создаёт рабочую папку вида 2026-06-21T12-00-00Z.building.
    /// Такая папка ещё не считается готовой эпохой.
    /// </summary>
    public string CreateBuilding(DateTimeOffset? utcTime = null)
    {
        Directory.CreateDirectory(_rootPath);

        var utc = (utcTime ?? DateTimeOffset.UtcNow).ToUniversalTime();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidateUtc = utc.AddSeconds(attempt);
            var stamp = FormatName(candidateUtc);
            var buildingPath = Path.Combine(_rootPath, stamp + BuildingSuffix);
            var readyPath = Path.Combine(_rootPath, stamp + ReadySuffix);

            if (Directory.Exists(buildingPath) || Directory.Exists(readyPath))
                continue;

            Directory.CreateDirectory(buildingPath);
            return Make(buildingPath, candidateUtc, EpochState.Building).Path;
        }

        throw new IOException("Could not create a unique building epoch folder.");
    }


    
    /// <summary>
    /// Последним действием переводит .building папку в .ready.
    /// Снаружи до этого должны быть сделаны запись, Flush/Close и проверка БД.
    /// </summary>
    public string MarkReady(string path)
    {
        var building = TryRead(path, BuildingSuffix);
        if(building == null) 
            throw new IOException($"Could not read building epoch folder: {path}");
        if (building.State != EpochState.Building)
            throw new ArgumentException("Only building epoch can be marked ready.", nameof(building));

        var sourcePath = Path.GetFullPath(building.Path);
        if (!IsUnderRoot(sourcePath))
            throw new ArgumentException("Epoch folder is outside root path.", nameof(building));

        var readyPath = Path.Combine(_rootPath, FormatName(building.UtcTime) + ReadySuffix);
        if (!Directory.Exists(sourcePath)) throw new DirectoryNotFoundException(sourcePath);
        if (Directory.Exists(readyPath)) throw new IOException($"Ready epoch already exists: {readyPath}");

        Directory.Move(sourcePath, readyPath);
        return Make(readyPath, building.UtcTime, EpochState.Ready).Path;
    }

    private static EpochFolderData? TryRead(string path, string suffix)
    {
        var name = Path.GetFileName(path);
        if (!name.EndsWith(suffix, StringComparison.Ordinal)) return null;

        var stamp = name[..^suffix.Length];
        if (!DateTimeOffset.TryParseExact(
                stamp,
                NameFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utc))
            return null;

        var state = suffix == ReadySuffix ? EpochState.Ready : EpochState.Building;
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

    private static EpochFolderData Make(string path, DateTimeOffset utc, EpochState state)
    {
        return new EpochFolderData(Path.GetFileName(path), path, utc, state);
    }

    private static string FormatName(DateTimeOffset utc)
    {
        return utc.ToUniversalTime().ToString(NameFormat, CultureInfo.InvariantCulture);
    }
}