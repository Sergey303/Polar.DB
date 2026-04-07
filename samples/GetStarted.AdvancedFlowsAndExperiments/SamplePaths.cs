namespace GetStarted.AdvancedFlowsAndExperiments;

internal static class SamplePaths
{
    public static string Root { get; } = EnsureWithSlash(Path.Combine(AppContext.BaseDirectory, "data", "AdvancedFlowsAndExperiments"));

    public static string Combine(string segment) => EnsureWithSlash(Path.Combine(Root, segment));

    public static void EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    private static string EnsureWithSlash(string path)
    {
        Directory.CreateDirectory(path);
        return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
    }
}
