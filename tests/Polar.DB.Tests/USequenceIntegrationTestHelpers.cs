namespace Polar.DB.Tests;

internal static class USequenceIntegrationTestHelpers
{
    internal static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)),
        new NamedType("tags", new PTypeSequence(new PType(PTypeEnumeration.sstring))));

    internal static object[] Row(int id, string name, int age, params string[] tags) =>
        new object[] { id, name, age, tags.Cast<object>().ToArray() };

    internal static IEnumerable<IComparable> TagsOf(object record) =>
        ((object[])((object[])record)[3]).Cast<IComparable>();

    internal static int IdOf(object record) => (int)((object[])record)[0];

    internal static string NameOf(object record) => (string)((object[])record)[1];

    internal static long InnerCount(USequence sequence) => sequence.sequence.Count();

    internal static long InnerAppendOffset(USequence sequence) => sequence.sequence.AppendOffset;

    internal static (long Count, long AppendOffset) ReadStateFile(string stateFilePath)
    {
        using var fs = new FileStream(stateFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);
        return (br.ReadInt64(), br.ReadInt64());
    }

    internal sealed class DeterministicIndexedSequenceEnvironment : IDisposable
    {
        private readonly string _tempDir;
        private readonly List<USequence> _created = new();

        public DeterministicIndexedSequenceEnvironment()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            StateFilePath = Path.Combine(_tempDir, "state.bin");
        }

        public string StateFilePath { get; }

        public USequence CreateSequenceWithIndexes(bool optimise = false)
        {
            int counter = 0;
            Stream StreamGen()
            {
                return new FileStream(
                    Path.Combine(_tempDir, $"part_{counter++}.bin"),
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite);
            }

            var sequence = new USequence(
                RecordType,
                StateFilePath,
                StreamGen,
                isEmpty: record => string.IsNullOrEmpty((string)((object[])record)[1]),
                keyFunc: value => (int)((object[])value)[0],
                hashOfKey: key => (int)key,
                optimise: optimise);

            var sIndex = new SVectorIndex(StreamGen, sequence, r => new[] { (string)((object[])r)[1] });
            var ageIndex = new UVectorIndex(StreamGen, sequence, new PType(PTypeEnumeration.integer),
                r => new IComparable[] { (int)((object[])r)[2] });
            var tagIndex = new UVecIndex(StreamGen, sequence, TagsOf, tag => Hashfunctions.HashRot13((string)tag), ignorecase: true);
            var exactNameIndex = new UIndex(
                StreamGen,
                sequence,
                applicable: _ => true,
                hashFunc: r => Hashfunctions.HashRot13((string)((object[])r)[1]),
                comp: Comparer<object>.Create((a, b) =>
                    string.Compare((string)((object[])a)[1], (string)((object[])b)[1], StringComparison.Ordinal)));

            sequence.uindexes = new IUIndex[] { sIndex, ageIndex, tagIndex, exactNameIndex };
            _created.Add(sequence);
            return sequence;
        }

        public void Dispose()
        {
            foreach (var sequence in _created)
            {
                try { sequence.Close(); }
                catch
                {
                    // ignored
                }
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
}
