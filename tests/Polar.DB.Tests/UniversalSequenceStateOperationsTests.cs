using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceStateOperationsTests
{
    [Fact]
    public void ValidateAndReload_KeepConsistentStorageBytesUnchanged()
    {
        using var stream = new MemoryStream();
        using var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();
        byte[] before = stream.ToArray();
        stream.Position = 4;

        sequence.ValidateStorageIntegrity();
        Assert.Equal(4, stream.Position);
        Assert.Equal(before, stream.ToArray());

        sequence.ReloadStateFromStorage();
        Assert.Equal(2, sequence.Count());
        Assert.Equal(16, sequence.AppendOffset);
        Assert.Equal(before, stream.ToArray());
    }

    [Fact]
    public void ValidateAndReload_RejectInconsistentFixedLengthWithoutRepair()
    {
        using var stream = new MemoryStream();
        using var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();
        UniversalSequenceBaseTestHelpers.WriteHeader(stream, 3);
        byte[] corrupted = stream.ToArray();

        Assert.Throws<InvalidDataException>(sequence.ValidateStorageIntegrity);
        Assert.Equal(corrupted, stream.ToArray());
        Assert.Throws<InvalidDataException>(sequence.ReloadStateFromStorage);
        Assert.Equal(corrupted, stream.ToArray());

        sequence.RecoverConsistentState();
        sequence.ValidateStorageIntegrity();
        Assert.Equal(2, sequence.Count());
        Assert.Equal(2, UniversalSequenceBaseTestHelpers.HeaderCount(stream));
        Assert.Equal(20, sequence.GetByIndex(1));
    }

    [Fact]
    public void Validate_DetectsTrailingVariableRecord_AndRecoverDiscardsIt()
    {
        using var stream = new MemoryStream();
        var type = UniversalSequenceBaseTestHelpers.CreateVariableSequenceType();
        using var sequence = new UniversalSequenceBase(type, stream);
        sequence.AppendElement(new object[] { 1, "one" });
        sequence.Flush();
        long committedTail = stream.Length;
        UniversalSequenceBaseTestHelpers.AppendSerializedTail(stream, type, new object[] { 2, "two" });
        byte[] before = stream.ToArray();

        Assert.Throws<InvalidDataException>(sequence.ValidateStorageIntegrity);
        Assert.Equal(before, stream.ToArray());
        Assert.Throws<InvalidDataException>(sequence.ReloadStateFromStorage);

        sequence.RecoverConsistentState();
        sequence.ValidateStorageIntegrity();
        Assert.Equal(committedTail, stream.Length);
        Assert.Equal(1, sequence.Count());
        Assert.Equal("one", (string)((object[])sequence.GetElement(8L))[1]);
    }

    [Fact]
    public void RecoverConsistentState_LeavesValidRecordsAvailable()
    {
        using var stream = new MemoryStream();
        using var sequence = UniversalSequenceBaseTestHelpers.CreateFixedIntSequence(stream);
        sequence.AppendElement(11);
        sequence.AppendElement(22);
        sequence.Flush();
        sequence.RecoverConsistentState();

        Assert.Equal(2, sequence.Count());
        Assert.Equal(11, sequence.GetByIndex(0));
        Assert.Equal(22, sequence.GetByIndex(1));
    }
}
