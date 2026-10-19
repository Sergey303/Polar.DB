namespace Polar.DB;

/// <summary>
/// A separately persisted checkpoint for the physical sequence, not for its indexes.
/// All checkpoints are optional hints: an invalid or missing checkpoint triggers recovery.
/// </summary>
internal static class SequenceCheckpointStore
{
    private const int Magic = 0x50445343; // PDSC
    private const int Version = 1;
    private const long CheckpointLength = sizeof(int) * 2 + sizeof(long) * 2;

    internal static string? ResolvePath(Stream media, string? path)
    {
        if (path != null)
            return Path.GetFullPath(path);

        return media is FileStream file
            ? Path.ChangeExtension(file.Name, ".state")
            : null;
    }

    internal static bool TryRead(string? path, out long count, out long appendOffset)
    {
        count = appendOffset = 0;
        if (path == null || !File.Exists(path))
            return false;

        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (file.Length != CheckpointLength)
                return false;
            using var reader = new BinaryReader(file);
            if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version)
                return false;
            count = reader.ReadInt64();
            appendOffset = reader.ReadInt64();
            return count >= 0 && appendOffset >= sizeof(long);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Legacy USequence state.bin begins with the same two numbers, but those numbers
    // represent an index checkpoint. This is a one-time fast-open fallback only.
    internal static bool TryReadLegacy(string? path, out long count, out long appendOffset)
    {
        count = appendOffset = 0;
        if (path == null || !File.Exists(path))
            return false;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (file.Length < sizeof(long) * 2)
                return false;
            using var reader = new BinaryReader(file);
            count = reader.ReadInt64();
            appendOffset = reader.ReadInt64();
            return count >= 0 && appendOffset >= sizeof(long);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static void Save(string? path, long count, long appendOffset)
    {
        if (path == null)
            return;
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Publish only a complete checkpoint. A crash leaves either the old or new file.
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(file))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(count);
                writer.Write(appendOffset);
                writer.Flush();
                file.Flush(flushToDisk: true);
            }
            // File.Move(source, destination, overwrite) is unavailable on netstandard2.1.
            // Replacing an existing file atomically also avoids a delete/move gap.
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
