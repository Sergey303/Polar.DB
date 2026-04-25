using System.Text;

namespace Polar.DB.Tests;

/// <summary>
/// Low-level helpers for storage corruption, recovery, durability, and file-backed storage tests.
///
/// This file intentionally combines two generations of helper APIs:
/// - the current stream-based helpers used by the newer contract tests;
/// - the older file/state helpers still referenced by crash-recovery and USequence state tests.
///
/// That keeps a mixed local test set compilable while the tests are being synchronized.
/// </summary>
internal static class StorageCorruptionHelpers
{
    /// <summary>
    /// Canonical fixed-size 32-bit integer item type used by tests.
    /// </summary>
    public static PType Int32Type { get; } = new PType(PTypeEnumeration.integer);

    /// <summary>
    /// Canonical fixed-size 64-bit integer item type used by tests.
    /// </summary>
    public static PType Int64Type { get; } = new PType(PTypeEnumeration.longinteger);

    /// <summary>
    /// Creates the variable-size record type used by rewrite and recovery tests.
    /// </summary>
    public static PType CreateVariableRecordType()
    {
        return new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));
    }

    /// <summary>
    /// Creates a UniversalSequenceBase over 32-bit integers.
    /// </summary>
    public static UniversalSequenceBase CreateInt32Sequence(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return new UniversalSequenceBase(Int32Type, stream);
    }

    /// <summary>
    /// Creates a UniversalSequenceBase over 64-bit integers.
    /// </summary>
    public static UniversalSequenceBase CreateInt64Sequence(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return new UniversalSequenceBase(Int64Type, stream);
    }

    /// <summary>
    /// Creates a UniversalSequenceBase over the shared variable-size record type.
    /// </summary>
    public static UniversalSequenceBase CreateVariableRecordSequence(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return new UniversalSequenceBase(CreateVariableRecordType(), stream);
    }

    /// <summary>
    /// Reads the declared header count without permanently changing stream position.
    /// </summary>
    public static long ReadHeaderCount(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        stream.Position = 0L;

        using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true);
        long count = reader.ReadInt64();
        return count;
    }

    /// <summary>
    /// Overwrites the declared header count without permanently changing stream position.
    /// </summary>
    public static void WriteHeaderCount(Stream stream, long count)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        stream.Position = 0L;

        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(count);
        writer.Flush();

    }

    /// <summary>
    /// Appends arbitrary bytes to the physical end of a stream.
    /// </summary>
    public static void AppendRawBytes(Stream stream, params byte[] bytes)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));

        stream.Position = stream.Length;
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    /// <summary>
    /// Appends one trailing Int32 value after the current physical end of stream.
    /// </summary>
    public static void AppendInt32Tail(Stream stream, int value)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        stream.Position = stream.Length;

        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(value);
        writer.Flush();
    }

    /// <summary>
    /// Appends one serialized value after the current physical end of stream.
    /// </summary>
    public static void AppendSerializedTail(Stream stream, PType type, object value)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (type == null) throw new ArgumentNullException(nameof(type));

        stream.Position = stream.Length;

        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        ByteFlow.Serialize(writer, value, type);
        writer.Flush();
    }

    /// <summary>
    /// Returns a byte-for-byte snapshot of a memory stream.
    /// </summary>
    public static byte[] SnapshotBytes(MemoryStream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return stream.ToArray();
    }

    // ---------------------------------------------------------------------
    // Compatibility helpers for older crash/state tests still present locally
    // ---------------------------------------------------------------------

    /// <summary>
    /// Writes a fixed-size integer sequence payload directly into a stream:
    /// [Int64 declaredCount][Int32 value0][Int32 value1]...
    /// Optional trailing bytes may be appended to simulate stale tail garbage.
    /// </summary>
    public static void WriteFixedSequenceBytes(
        Stream stream,
        IEnumerable<int> values,
        long declaredCount,
        byte[]? trailingBytes = null)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (values == null) throw new ArgumentNullException(nameof(values));

        stream.SetLength(0L);
        stream.Position = 0L;

        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(declaredCount);

        foreach (int value in values)
        {
            writer.Write(value);
        }

        if (trailingBytes is { Length: > 0 })
        {
            writer.Write(trailingBytes);
        }

        writer.Flush();
        stream.Position = 0L;
    }

    /// <summary>
    /// Builds a raw byte representation of a variable-size string sequence:
    /// [Int64 declaredCount][serialized string 0][serialized string 1]...
    /// </summary>
    public static byte[] BuildVariableStringSequenceBytes(params string[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);

        writer.Write((long)values.Length);

        var type = new PType(PTypeEnumeration.sstring);
        foreach (string value in values)
        {
            ByteFlow.Serialize(writer, value, type);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Concatenates arbitrary byte arrays in order.
    /// </summary>
    public static byte[] ConcatBytes(params byte[][] parts)
    {
        if (parts == null) throw new ArgumentNullException(nameof(parts));

        int totalLength = 0;
        foreach (byte[] part in parts)
        {
            if (part != null)
            {
                totalLength += part.Length;
            }
        }

        byte[] result = new byte[totalLength];
        int offset = 0;

        foreach (byte[] part in parts)
        {
            if (part == null || part.Length == 0)
            {
                continue;
            }

            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    /// <summary>
    /// Creates a unique temporary directory and returns its path.
    /// </summary>
    public static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "PolarDbTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Best-effort recursive delete that must not hide the original test failure.
    /// </summary>
    public static void DeleteDirectoryQuietly(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // cleanup must not hide test failure
        }
    }

    /// <summary>
    /// Creates a file-backed integer UniversalSequenceBase.
    /// </summary>
    public static UniversalSequenceBase CreateIntegerUniversalSequence(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return new UniversalSequenceBase(
            Int32Type,
            new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
    }

    /// <summary>
    /// Creates a file-backed string UniversalSequenceBase.
    /// </summary>
    public static UniversalSequenceBase CreateStringUniversalSequence(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return new UniversalSequenceBase(
            new PType(PTypeEnumeration.sstring),
            new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
    }

    /// <summary>
    /// Corrupts the fixed-size header count in a file-backed sequence.
    /// </summary>
    public static void CorruptHeaderCount(string path, long count)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        WriteHeaderCount(stream, count);
    }

    /// <summary>
    /// Appends arbitrary bytes to a file-backed sequence stream.
    /// </summary>
    public static void AppendBytes(string path, byte[] bytes)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));

        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        stream.Position = stream.Length;
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    /// <summary>
    /// Creates a file-backed integer USequence with a sidecar state file.
    /// Each internal stream request gets its own file inside tempDir.
    /// </summary>
    public static USequence CreateIntegerSequence(string tempDir, string statePath, bool optimise = false)
    {
        if (tempDir == null) throw new ArgumentNullException(nameof(tempDir));
        if (statePath == null) throw new ArgumentNullException(nameof(statePath));

        Directory.CreateDirectory(tempDir);

        int counter = 0;

        Stream StreamGen()
        {
            string streamPath = Path.Combine(tempDir, $"useq_{counter++:D4}.bin");
            return new FileStream(streamPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        return new USequence(
            new PType(PTypeEnumeration.integer),
            statePath,
            StreamGen,
            _ => false,
            value => (int)value,
            key => (int)key,
            optimise: optimise);
    }

    /// <summary>
    /// Reads the two Int64 values written into the sidecar state file.
    /// </summary>
    public static (long Count, long AppendOffset) ReadState(string statePath)
    {
        if (statePath == null) throw new ArgumentNullException(nameof(statePath));

        using var fs = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(fs);

        long count = reader.ReadInt64();
        long appendOffset = reader.ReadInt64();
        return (count, appendOffset);
    }

    /// <summary>
    /// Writes a deliberately too-short state file to simulate a torn or truncated sidecar write.
    /// </summary>
    public static void WriteTooShortState(string statePath)
    {
        if (statePath == null) throw new ArgumentNullException(nameof(statePath));

        string? dir = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(statePath, new byte[] { 0x01, 0x02, 0x03 });
    }

    /// <summary>
    /// Temporary test directory owner. Cleanup failures are swallowed so they do not hide test failures.
    /// </summary>
    public sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "PolarDbTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string File(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            return System.IO.Path.Combine(Path, fileName);
        }

        public FileStream Open(
            string fileName,
            FileMode mode = FileMode.OpenOrCreate,
            FileShare share = FileShare.ReadWrite)
        {
            return new FileStream(
                File(fileName),
                mode,
                FileAccess.ReadWrite,
                share);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Cleanup must not hide the original assertion failure.
            }
        }
    }
}