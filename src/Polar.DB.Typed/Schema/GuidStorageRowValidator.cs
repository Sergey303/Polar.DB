using System.Text;

namespace Polar.DB.Typed.Schema;

internal static class GuidStorageRowValidator
{
    internal static void Validate(string dataPath, PType recordType,
        IReadOnlyList<FieldScheme> fields, string tablePath, string schemaPath)
    {
        try
        {
            using var stream = new FileStream(dataPath, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            if (stream.Length < sizeof(long) || reader.ReadInt64() < 0)
                throw new InvalidDataException("Invalid sequence header.");

            byte[] expected = new byte[4096];
            byte[] actual = new byte[4096];
            while (stream.Position < stream.Length)
            {
                long start = stream.Position;
                // First check each serialized length against the remaining bytes.
                ByteFlow.Skip(reader, recordType);
                long end = stream.Position;
                if (end <= start)
                    throw new InvalidDataException("Zero-length record.");

                stream.Position = start;
                object[] row = (object[])ByteFlow.Deserialize(reader, recordType);
                foreach (FieldScheme field in fields.Where(f => f.ClrType == typeof(Guid)))
                {
                    object stored = row[field.Index];
                    if (field.PolarType.Vid == PTypeEnumeration.sstring)
                        _ = Guid.ParseExact((string)stored, "D");
                    else if (stored is not Guid)
                        throw new InvalidDataException("Non-Guid value in native Guid field.");
                }

                using var copy = new MemoryStream();
                using (var writer = new BinaryWriter(copy, Encoding.UTF8, leaveOpen: true))
                {
                    ByteFlow.Serialize(writer, row, recordType);
                    writer.Flush();
                }

                if (copy.Length != end - start)
                    throw new InvalidDataException("Serialized record length mismatch.");

                copy.Position = 0;
                stream.Position = start;
                while (copy.Position < copy.Length)
                {
                    int size = (int)Math.Min(expected.Length, copy.Length - copy.Position);
                    copy.ReadExactly(expected.AsSpan(0, size));
                    stream.ReadExactly(actual.AsSpan(0, size));
                    if (!expected.AsSpan(0, size).SequenceEqual(actual.AsSpan(0, size)))
                        throw new InvalidDataException("Record cannot round-trip through the declared layout.");
                }

                stream.Position = end;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException
            or FormatException or OverflowException or InvalidCastException)
        {
            throw new SchemeCompatibilityException(tablePath, schemaPath,
                "Existing persisted records do not match the declared Guid layout: " +
                ex.Message);
        }
    }
}
