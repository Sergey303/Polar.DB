namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// расширения EpochFolderManager
/// </summary>
public static class StorageEpochFolderManager
{
    /// <summary>
    /// Создаёт сущность типа T в новой папке или открывает из старой папки последней эпохи.
    /// </summary>
    /// <param name="epochFolderManager"></param>
    /// <param name="factory">(path, isNew)=>T</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T OpenLastOrCreateNewEpoch<T>(this EpochFolderManager epochFolderManager,  Func<string, bool, T> factory)
        where T: IDisposable
    {
        // ищем путь старой БД именно .ready, игнорируем папки .building
        var latestReadyPath = epochFolderManager.GetLatestReady();
        if (!string.IsNullOrWhiteSpace(latestReadyPath) && Directory.Exists(latestReadyPath))
            return factory(latestReadyPath, false);

        // если нет, создаём пустую как новую эпоху
        return epochFolderManager.DoNewEpoch(factory);
    }   
    
    /// <summary>
    /// Создаёт новую сущность типа T в новой папке.
    /// </summary>
    /// <param name="epochFolderManager"></param>
    /// <param name="factory">(path, isNew)=>T</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T DoNewEpoch<T>(this EpochFolderManager epochFolderManager,  Func<string, bool, T> factory)
        where T: IDisposable
    {
        
        // новый путь заканчивается на .building, пустая папка
        var newBuildingPath = epochFolderManager.CreateBuilding(DateTimeOffset.UtcNow);
        // пустая БД в папке, которая заканчивается на .building, она не считается завершённой и, если что-то сломается в этом методе, папка будет игнорироваться.
        // второй параметр isNew заполняется true, при этом внутри factory нужно заполнить строки - они копируютсяиз старой БД или нулевая эпоха может быть пустая. 
        var newStorage = factory(newBuildingPath, true);
            
        // не мешаем переименовывать
        newStorage.Dispose();
            
        // переименовываем .building в  .ready;
        var latestReadyPath = epochFolderManager.MarkReady(newBuildingPath);
        // Asset.Equals oldDbPath == epochFolderManager.GetLatestReady()
        // return epochFolderManager.OpenLastOrCreateStorage<T>(factory);
        // Console.WriteLine("Создано новое поколение.");
        
        return factory(latestReadyPath, false);
    }
}