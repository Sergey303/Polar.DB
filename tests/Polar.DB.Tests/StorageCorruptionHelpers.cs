using System.Text;

namespace Polar.DB.Tests;

/// <summary>
/// Provides low-level helpers for storage corruption, recovery, and durability tests.
/// </summary>
/// <remarks>
/// The helpers intentionally operate at stream and binary-header level. This keeps corruption scenarios explicit and
/// makes test intent clear when a case simulates stale tails, truncated records, mismatched header counts, or restart
/// through a real file-backed stream.
/// </remarks>
internal static class StorageCorruptionHelpers
{
    /// <summary>
    /// Gets the canonical 32-bit integer item type used by fixed-size sequence tests.
    /// </summary>
    public static PType Int32Type { get; } = new PType(PTypeEnumeration.integer);

    /// <summary>
    /// Gets the canonical 64-bit integer item type used by fixed-size sequence tests and throughput smoke tests.
    /// </summary>
    public static PType Int64Type { get; } = new PType(PTypeEnumeration.longinteger);

    /// <summary>
    /// Creates the variable-size record type used by recovery and rewrite tests.
    /// </summary>
    /// <returns>
    /// A record type with an integer <c>id</c> field and a variable-size string <c>name</c> field.
    /// </returns>
    public static PType CreateVariableRecordType()
    {
        return new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));
    }

    /// <summary>
    /// Creates a <see cref="UniversalSequenceBase"/> over 32-bit integer items.
    /// </summary>
    /// <param name="stream">The stream that contains or will contain the sequence data.</param>
    /// <returns>A sequence instance bound to <paramref name="stream"/>.</returns>
    public static UniversalSequenceBase CreateInt32Sequence(Stream stream)
    {
        return new UniversalSequenceBase(Int32Type, stream);
    }

    /// <summary>
    /// Creates a <see cref="UniversalSequenceBase"/> over 64-bit integer items.
    /// </summary>
    /// <param name="stream">The stream that contains or will contain the sequence data.</param>
    /// <returns>A sequence instance bound to <paramref name="stream"/>.</returns>
    public static UniversalSequenceBase CreateInt64Sequence(Stream stream)
    {
        return new UniversalSequenceBase(Int64Type, stream);
    }

    /// <summary>
    /// Creates a <see cref="UniversalSequenceBase"/> over the shared variable-size test record type.
    /// </summary>
    /// <param name="stream">The stream that contains or will contain the sequence data.</param>
    /// <returns>A variable-size record sequence instance bound to <paramref name="stream"/>.</returns>
    public static UniversalSequenceBase CreateVariableRecordSequence(Stream stream)
    {
        return new UniversalSequenceBase(CreateVariableRecordType(), stream);
    }

    /// <summary>
    /// Reads the declared element count stored in the sequence header without permanently changing stream position.
    /// </summary>
    /// <param name="stream">The sequence stream whose first eight bytes contain the declared count.</param>
    /// <returns>The 64-bit count value currently stored in the sequence header.</returns>
    public static long ReadHeaderCount(Stream stream)
    {
        long saved = stream.Position;
        stream.Position = 0L;
        using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true);
        long count = reader.ReadInt64();
        stream.Position = Math.Min(saved, stream.Length);
        return count;
    }

    /// <summary>
    /// Writes a declared element count into the sequence header without permanently changing stream position.
    /// </summary>
    /// <param name="stream">The sequence stream whose header should be overwritten.</param>
    /// <param name="count">The count value to store in the first eight bytes of the stream.</param>
    /// <remarks>
    /// This method is deliberately used to create mismatches between declared count and readable data during crash
    /// simulation tests.
    /// </remarks>
    public static void WriteHeaderCount(Stream stream, long count)
    {
        long saved = stream.Position;
        stream.Position = 0L;
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(count);
        writer.Flush();
        stream.Position = Math.Min(saved, stream.Length);
    }

    /// <summary>
    /// Appends arbitrary bytes to the physical end of a stream.
    /// </summary>
    /// <param name="stream">The stream to corrupt or extend.</param>
    /// <param name="bytes">The raw bytes to append.</param>
    /// <remarks>
    /// The appended bytes may represent stale tail data, incomplete serialized values, or intentionally invalid data.
    /// </remarks>
    public static void AppendRawBytes(Stream stream, params byte[] bytes)
    {
        stream.Position = stream.Length;
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    /// <summary>
    /// Appends one serialized 32-bit integer after the current logical stream contents.
    /// </summary>
    /// <param name="stream">The stream to extend with a stale integer tail.</param>
    /// <param name="value">The integer value to append.</param>
    public static void AppendInt32Tail(Stream stream, int value)
    {
        stream.Position = stream.Length;
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(value);
        writer.Flush();
    }

    /// <summary>
    /// Appends a value serialized according to a Polar type after the current logical stream contents.
    /// </summary>
    /// <param name="stream">The stream to extend.</param>
    /// <param name="type">The Polar type used to serialize <paramref name="value"/>.</param>
    /// <param name="value">The value to serialize as stale or extra physical data.</param>
    public static void AppendSerializedTail(Stream stream, PType type, object value)
    {
        stream.Position = stream.Length;
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        ByteFlow.Serialize(writer, value, type);
        writer.Flush();
    }

    /// <summary>
    /// Creates a byte-for-byte snapshot of a memory-backed sequence stream.
    /// </summary>
    /// <param name="stream">The memory stream to snapshot.</param>
    /// <returns>A new byte array containing the current stream contents.</returns>
    public static byte[] SnapshotBytes(MemoryStream stream)
    {
        return stream.ToArray();
    }

    /// <summary>
    /// Owns a temporary directory used by file-backed storage tests.
    /// </summary>
    /// <remarks>
    /// The directory is deleted during disposal. Cleanup failures are intentionally swallowed so that filesystem cleanup
    /// noise does not hide the original test assertion failure.
    /// </remarks>
    public sealed class TempDirectory : IDisposable
    {
        /// <summary>
        /// Creates a new unique temporary directory for one test case.
        /// </summary>
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Gets the full path of the temporary directory.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Combines the temporary directory path with a test file name.
        /// </summary>
        /// <param name="fileName">The file name relative to the temporary directory.</param>
        /// <returns>The full path for <paramref name="fileName"/> inside this temporary directory.</returns>
        public string File(string fileName) => System.IO.Path.Combine(Path, fileName);

        /// <summary>
        /// Opens a read-write file stream inside the temporary directory.
        /// </summary>
        /// <param name="fileName">The file name relative to the temporary directory.</param>
        /// <param name="mode">The file mode used to open or create the file.</param>
        /// <param name="share">The file-sharing mode used to model locking behavior.</param>
        /// <returns>A read-write <see cref="FileStream"/> for the requested test file.</returns>
        public FileStream Open(string fileName, FileMode mode = FileMode.OpenOrCreate, FileShare share = FileShare.ReadWrite)
        {
            return new FileStream(File(fileName), mode, FileAccess.ReadWrite, share);
        }

        /// <summary>
        /// Deletes the temporary directory and all files created inside it.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test cleanup should not hide the original assertion failure.
            }
        }
    }
}
