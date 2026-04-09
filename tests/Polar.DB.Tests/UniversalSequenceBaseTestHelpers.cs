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

    public static void AppendPeople(UniversalSequenceBase sequence, params object[][] rows)
    {
        foreach (var row in rows)
            sequence.AppendElement(row);
    }

    public static string CreateTempFilePath(string fileName)
    {
        string dir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    public static BinaryWriter CreateTailWriter(Stream stream)
    {
        return new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
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
            }
        }
    }
}
