namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Small helpers for storage objects that can be opened inside an epoch folder.
/// EpochFolderManager still owns only folders and ready markers.
/// </summary>
public static class StorageEpochFolderManager
{
    public static T OpenLastOrCreateNewEpoch<T>(
        this EpochFolderManager epochs,
        Func<string, bool, T> factory)
        where T : IDisposable
    {
        if (epochs == null) throw new ArgumentNullException(nameof(epochs));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        var latestReadyPath = epochs.GetLatestReady();
        if (!string.IsNullOrWhiteSpace(latestReadyPath) && Directory.Exists(latestReadyPath))
            return factory(latestReadyPath, false);

        return epochs.CreateReadyEpoch(factory);
    }

    public static T CreateReadyEpoch<T>(
        this EpochFolderManager epochs,
        Func<string, bool, T> factory)
        where T : IDisposable
    {
        if (epochs == null) throw new ArgumentNullException(nameof(epochs));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        var epochPath = epochs.CreateBuilding(DateTimeOffset.UtcNow);
        using (factory(epochPath, true))
        {
        }

        var readyPath = epochs.MarkReady(epochPath);
        return factory(readyPath, false);
    }
}
