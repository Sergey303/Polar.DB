namespace Polar.DB.Tests;

public static class UKeyIndexTestHelpers
{
    internal static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    internal sealed class SequenceScope : IDisposable
    {
        private readonly string _tempDir;
        private int _fileNo;

        public SequenceScope()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            Sequence = new USequence(
                RecordType,
                Path.Combine(_tempDir, "state.bin"),
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);
        }

        public USequence Sequence { get; }

        public Stream StreamGen()
        {
            return new FileStream(
                Path.Combine(_tempDir, $"f{_fileNo++}.bin"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
        }

        public void Dispose()
        {
            try { Sequence.Close(); }
            catch
            {
                // ignored
            }

            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignored
            }
        }
    }

    internal static object[] Row(int id, string name) => new object[] { id, name };

    internal static int IdOf(object record) => (int)((object[])record)[0];

    internal static string NameOf(object record) => (string)((object[])record)[1];

    internal static void LoadAndBuild(SequenceScope scope, UKeyIndex index, params object[][] rows)
    {
        scope.Sequence.Load(rows.Cast<object>().ToArray());
        index.Build();
    }

}
