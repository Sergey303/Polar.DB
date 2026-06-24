namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Удобные расширения EpochFolderManager для простых сценариев.
/// </summary>
public static class StorageEpochFolderManager
{
    /// <summary>
    /// Открывает последнюю готовую эпоху или создаёт первую.
    /// </summary>
    public static T OpenLastOrCreateNewEpoch<T>(
        this EpochFolderManager epochFolderManager,
        Func<string, bool, T> factory)
        where T : IDisposable
    {
        var latestReadyPath = epochFolderManager.GetLatestReady();
        if (!string.IsNullOrWhiteSpace(latestReadyPath) && Directory.Exists(latestReadyPath))
            return factory(latestReadyPath, false);

        return epochFolderManager.DoNewEpoch(factory);
    }

    /// <summary>
    /// Создаёт новую эпоху, закрывает созданную сущность и ставит marker готовности.
    /// </summary>
    public static T DoNewEpoch<T>(
        this EpochFolderManager epochFolderManager,
        Func<string, bool, T> factory)
        where T : IDisposable
    {
        var epochPath = epochFolderManager.CreateBuilding(DateTimeOffset.UtcNow);
        var newStorage = factory(epochPath, true);

        newStorage.Dispose();

        var latestReadyPath = epochFolderManager.MarkReady(epochPath);
        return factory(latestReadyPath, false);
    }
}
