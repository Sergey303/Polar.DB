namespace PolarDbBenchmarks;

internal static class BenchmarkFileWarmup
{
    public static void ReadAll(string dir)
    {
        if (!Directory.Exists(dir)) return;

        var buffer = new byte[1024 * 1024];
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            while (stream.Read(buffer, 0, buffer.Length) > 0)
            {
            }
        }
    }
}
