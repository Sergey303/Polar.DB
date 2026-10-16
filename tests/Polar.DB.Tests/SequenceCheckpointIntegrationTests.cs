using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class SequenceCheckpointIntegrationTests
{
    private static readonly PTypeRecord RowType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    [Fact]
    public void FileBackedSequence_ReopensFromItsOwnCheckpoint()
    {
        string directory = CreateTemp();
        try
        {
            string data = Path.Combine(directory, "f0.bin");
            using (var writer = OpenBase(data))
            {
                writer.AppendElement(new object[] { 1, "one" });
                writer.AppendElement(new object[] { 2, "second" });
                writer.Flush();
            }

            Assert.True(File.Exists(Path.ChangeExtension(data, ".state")));
            using var reopened = OpenBase(data);
            Assert.Equal(2, reopened.Count());
            Assert.Equal(new[] { 1, 2 }, reopened.ElementValues()
                .Cast<object[]>()
                .Select(value => (int)value[0])
                .ToArray());
            Assert.Equal(new FileInfo(data).Length, reopened.AppendOffset);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void MissingOrMalformedCheckpoint_RecoversAndPersistsNewCheckpoint()
    {
        string directory = CreateTemp();
        try
        {
            string data = Path.Combine(directory, "f0.bin");
            string state = Path.ChangeExtension(data, ".state");
            using (var writer = OpenBase(data))
            {
                writer.AppendElement(new object[] { 1, "alpha" });
                writer.AppendElement(new object[] { 2, "beta" });
            }
            File.WriteAllBytes(state, new byte[] { 0xAB });
            using (var recovered = OpenBase(data))
            {
                Assert.Equal(2, recovered.Count());
                recovered.Flush();
            }
            Assert.Equal(24, new FileInfo(state).Length);
            File.Delete(state);
            using (var recovered = OpenBase(data))
            {
                Assert.Equal(2, recovered.Count());
                Assert.Equal("beta", (string)((object[])recovered.ElementValues().Last())[1]);
            }
            Assert.True(File.Exists(state));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void IndexedSequence_ReplaysAppendAfterLastIndexBuild()
    {
        string directory = CreateTemp();
        try
        {
            using (var writer = OpenIndexed(directory))
            {
                writer.AppendElement(new object[] { 1, "alpha" });
                writer.AppendElement(new object[] { 2, "beta" });
                writer.Build();
                writer.AppendElement(new object[] { 3, "gamma" });
            }
            Assert.True(File.Exists(Path.Combine(directory, "f0.state")));
            Assert.Equal(1, Directory.EnumerateFiles(directory, "*.state").Count());

            using (var reopened = OpenIndexed(directory))
            {
                reopened.Refresh();
                Assert.Equal("gamma", (string)((object[])reopened.GetByKey(3)!)[1]);
                Assert.Equal("alpha", (string)((object[])reopened.GetByKey(1)!)[1]);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void LegacyIndexCheckpoint_ReopensWithoutSidecarAndReplaysDynamicTail()
    {
        string directory = CreateTemp();
        try
        {
            using (var writer = OpenIndexed(directory))
            {
                writer.AppendElement(new object[] { 1, "one" });
                writer.Build();
                writer.AppendElement(new object[] { 2, "two" });
            }
            File.Delete(Path.Combine(directory, "f0.state"));
            using (var reader = OpenIndexed(directory))
            {
                reader.Refresh();
                Assert.Equal("two", (string)((object[])reader.GetByKey(2)!)[1]);
                Assert.Equal("one", (string)((object[])reader.GetByKey(1)!)[1]);
            }
            Assert.True(File.Exists(Path.Combine(directory, "f0.state")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static UniversalSequenceBase OpenBase(string path) =>
        new(RowType, new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite));

    private static USequence OpenIndexed(string directory)
    {
        int next = 0;
        Stream StreamGen() => new FileStream(Path.Combine(directory, $"f{next++}.bin"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite);
        var sequence = new USequence(RowType, Path.Combine(directory, "state.bin"),
            StreamGen, _ => false, optimise: false);
        sequence.SetPrimaryKey<int>(row => (int)((object[])row)[0]);
        return sequence;
    }

    private static string CreateTemp()
    {
        string directory = Path.Combine(Path.GetTempPath(), "polar-sequence-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
