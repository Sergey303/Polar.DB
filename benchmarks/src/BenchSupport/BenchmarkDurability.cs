namespace PolarDbBenchmarks;

internal static class BenchmarkDurability
{
    public static void SyncDirectoryFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith("-shm", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete);
                stream.Flush(flushToDisk: true);
            }
            catch (FileNotFoundException)
            {
                // SQLite can remove a truncated WAL between enumeration and open.
            }
        }
    }
}
