using System.Buffers.Binary;

namespace Polar.DB.ExternalKey;

internal static class ExternalKeyIndexKeyCodec<T>
    where T : IComparable<T>
{
    private const long SequenceHeaderSize = sizeof(long);
    private const int GuidBinarySize = 16;
    private const int LegacyGuidStringPayloadSize = 33;

    internal static PType GetStorageType(bool legacyGuidStringStorage)
    {
        Type type = typeof(T);
        if (type == typeof(int)) return new PType(PTypeEnumeration.integer);
        if (type == typeof(long)) return new PType(PTypeEnumeration.longinteger);
        if (type == typeof(string)) return new PType(PTypeEnumeration.sstring);
        if (type == typeof(Guid))
            return new PType(legacyGuidStringStorage
                ? PTypeEnumeration.sstring
                : PTypeEnumeration.guid);
        if (type == typeof(bool)) return new PType(PTypeEnumeration.boolean);

        throw new NotSupportedException($"ExternalKeyIndex does not support key type {type.FullName}.");
    }

    internal static object ToStorage(T key, bool legacyGuidStringStorage)
    {
        if (key is not Guid guid) return key;
        return legacyGuidStringStorage ? guid.ToString("N") : guid;
    }

    internal static T FromStorage(object value, bool legacyGuidStringStorage)
    {
        if (typeof(T) != typeof(Guid))
            return (T)value;

        if (!legacyGuidStringStorage)
            return (T)value;

        return (T)(object)Guid.Parse((string)value);
    }

    internal static bool IsLegacyGuidStringStorage(Stream stream)
    {
        if (typeof(T) != typeof(Guid) || stream.Length <= SequenceHeaderSize)
            return false;
        if (!stream.CanSeek || !stream.CanRead)
            throw new NotSupportedException("Guid external-index storage requires a seekable readable stream.");

        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0L;
            Span<byte> header = stackalloc byte[sizeof(long)];
            int totalRead = 0;
            while (totalRead < header.Length)
            {
                int read = stream.Read(header.Slice(totalRead));
                if (read <= 0) return false;
                totalRead += read;
            }

            long count = BinaryPrimitives.ReadInt64LittleEndian(header);
            if (count <= 0L) return false;

            long legacyLength = checked(SequenceHeaderSize + count * LegacyGuidStringPayloadSize);
            long nativeLength = checked(SequenceHeaderSize + count * GuidBinarySize);

            if (stream.Length == legacyLength) return true;
            if (stream.Length == nativeLength) return false;

            throw new InvalidDataException(
                "Guid external-index key storage matches neither the native 16-byte format " +
                "nor the legacy 32-character string format. Rebuild the derived index.");
        }
        finally
        {
            stream.Position = Math.Min(originalPosition, stream.Length);
        }
    }

    internal static T Cast(IComparable key)
    {
        if (key is T typed) return typed;
        if (typeof(T) == typeof(Guid) && key is string text)
            return (T)(object)Guid.Parse(text);
        if (typeof(T) == typeof(string))
            return (T)(object)Convert.ToString(key)!;
        if (typeof(T) == typeof(bool))
            return (T)(object)Convert.ToBoolean(key);

        return (T)Convert.ChangeType(key, typeof(T));
    }
}
