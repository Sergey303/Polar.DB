using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class SequenceCheckpointReplaceTests
{
    [Fact]
    public void Repeated_Flush_Atomically_Replaces_Checkpoint_On_All_Targets()
    {
        string root = Path.Combine(Path.GetTempPath(), "polar-checkpoint-replace-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string data = Path.Combine(root, "sequence.bin");
        string state = Path.ChangeExtension(data, ".state");
        var elementType = new PType(PTypeEnumeration.integer);

        try
        {
            using (var sequence = new UniversalSequenceBase(elementType,
                new FileStream(data, FileMode.OpenOrCreate, FileAccess.ReadWrite)))
            {
                sequence.AppendElement(1);
                sequence.Flush();
                Assert.Equal(1, ReadCheckpointCount(state));

                sequence.AppendElement(2);
                sequence.Flush();
                Assert.Equal(2, ReadCheckpointCount(state));

                sequence.Flush();
                Assert.Equal(2, ReadCheckpointCount(state));
            }

            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
            using var reopened = new UniversalSequenceBase(elementType,
                new FileStream(data, FileMode.Open, FileAccess.ReadWrite));
            Assert.Equal(2L, reopened.Count());
            Assert.Equal(new[] { 1, 2 }, reopened.ElementValues()
                .Select(value => (int)value).ToArray());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static long ReadCheckpointCount(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        Assert.Equal(0x50445343, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        return reader.ReadInt64();
    }
}
