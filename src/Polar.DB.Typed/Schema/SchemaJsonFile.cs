using System.Text;

namespace Polar.DB.Typed.Schema;

internal static class SchemaJsonFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static string Read(string path) => File.ReadAllText(path, Encoding.UTF8);

    public static void WriteAtomic(string path, string json)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Schema path '{path}' has no directory.");

        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        string backupPath = Path.Combine(directory, $".{Path.GetFileName(path)}.replace.bak");

        try
        {
            File.WriteAllText(tempPath, json, Utf8NoBom);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                TryDelete(backupPath);
                return;
            }

            File.Move(tempPath, path);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
