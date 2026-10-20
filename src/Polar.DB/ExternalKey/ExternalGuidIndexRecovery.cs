using System.Buffers.Binary;

namespace Polar.DB.ExternalKey;

// Only derived Guid index files may be reset: the primary record stream is never touched.
internal static class ExternalGuidIndexRecovery
{
    internal static bool CanRebuildKeyStream<T>(Stream stream)
    {
        if (typeof(T) != typeof(Guid) || !stream.CanSeek || !stream.CanRead || !stream.CanWrite ||
            stream.Length < sizeof(long))
            return false;

        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            ReadHeader(stream, bytes);
            long count = BinaryPrimitives.ReadInt64LittleEndian(bytes);
            long payload = stream.Length - sizeof(long);

            // A stale count, an unfinished element, or a cut-off final element
            // are recoverable. Reject implausible/unbounded counts and negative headers.
            return count >= 0 && count <= payload / 16 + 1;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    internal static bool HasInterruptedOffsets(Stream stream)
    {
        if (!stream.CanSeek || !stream.CanRead || !stream.CanWrite)
            throw new NotSupportedException("Guid index offsets require a readable, writable, seekable stream.");
        if (stream.Length == 0) return false;
        if (stream.Length < sizeof(long)) return true;

        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            ReadHeader(stream, bytes);
            long count = BinaryPrimitives.ReadInt64LittleEndian(bytes);
            if (count < 0)
                throw new InvalidDataException("Guid index offsets have a negative count.");
            long payload = stream.Length - sizeof(long);
            return payload % sizeof(long) != 0 || payload / sizeof(long) != count;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    internal static void Reset(Stream stream)
    {
        stream.SetLength(0);
        stream.Position = 0;
    }

    private static void ReadHeader(Stream stream, Span<byte> header)
    {
        int total = 0;
        while (total < header.Length)
        {
            int read = stream.Read(header.Slice(total));
            if (read <= 0) throw new InvalidDataException("Guid index header is truncated.");
            total += read;
        }
    }
}
