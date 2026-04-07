namespace GetStarted.SequencesAndIndexes;

internal static class SamplePaths
{
    public static string DataDirectory { get; } = CreateDataDirectory();

    private static string CreateDataDirectory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "SequencesAndIndexes");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "Databases"));
        return path;
    }

    public static string File(string name) => Path.Combine(DataDirectory, name);
    public static string DatabaseFile(string name) => Path.Combine(DataDirectory, "Databases", name);
}
