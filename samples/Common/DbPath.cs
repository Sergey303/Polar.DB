namespace Common;

public static class DbPath
{
    public static string Create()
    {
        string dbPath = $"{Path.GetTempPath()}\\data\\{Guid.NewGuid():N}\\";
        Directory.CreateDirectory(dbPath);
        return dbPath;
    }
}
