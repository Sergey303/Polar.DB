namespace GetStarted.TablesAndIndexes;

internal static class SamplePaths
{
    private static string? _root;

    public static string Root
    {
        get
        {
            if (_root is null)
            {
                var baseDir = Path.Combine(AppContext.BaseDirectory, "data", "TablesAndIndexes");
                Directory.CreateDirectory(baseDir);
                Directory.CreateDirectory(Path.Combine(baseDir, "Databases"));
                _root = baseDir + Path.DirectorySeparatorChar;
            }

            return _root;
        }
    }

    public static string File(string relativePath)
    {
        var normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var full = Path.Combine(Root, normalized);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return full;
    }
}
