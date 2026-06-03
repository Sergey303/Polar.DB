using System.Text;

namespace Polar.DB.Tests;

internal static class UniversalSequenceBaseTestHelpers
{
    public static UniversalSequenceBase CreateFixedLongSequence(Stream stream)
    {
        return new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), stream);
    }

    public static UniversalSequenceBase CreateFixedIntSequence(Stream stream)
    {
        return new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);
    }

    public static PType CreateVariableSequenceType()
    {
        return new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));
    }

    public static UniversalSequenceBase CreateVariableSequence(Stream stream)
    {
        return new UniversalSequenceBase(CreateVariableSequenceType(), stream);
    }

    public static BinaryWriter CreateTailWriter(Stream stream)
    {
        return new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
    }

    public static void WriteHeader(Stream stream, long count)
    {
        long saved = stream.Position;
        stream.Position = 0L;
        using (var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true))
        {
            writer.Write(count);
            writer.Flush();
        }

        stream.Position = Math.Min(saved, stream.Length);
    }

    public static void AppendFixedIntTail(Stream stream, int value)
    {
        stream.Position = stream.Length;
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(value);
        writer.Flush();
    }

    public static void AppendRawBytes(Stream stream, params byte[] bytes)
    {
        stream.Position = stream.Length;
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    public static void AppendSerializedTail(Stream stream, PType type, object value)
    {
        stream.Position = stream.Length;
        using var writer = CreateTailWriter(stream);
        ByteFlow.Serialize(writer, value, type);
        writer.Flush();
    }

    public static long HeaderCount(MemoryStream stream)
    {
        return BitConverter.ToInt64(stream.ToArray(), 0);
    }

    public sealed class TempFileScope : IDisposable
    {
        private readonly string _dir;

        public TempFileScope(string fileName)
        {
            _dir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            FilePath = Path.Combine(_dir, fileName);
        }

        public string FilePath { get; }

        public FileStream Open(FileMode mode = FileMode.OpenOrCreate)
        {
            return new FileStream(FilePath, mode, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, recursive: true);
            }
            catch
            {
                // ignored
            }
        }
    }
}
