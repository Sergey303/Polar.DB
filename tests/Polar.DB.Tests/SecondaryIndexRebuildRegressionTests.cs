using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class SecondaryIndexRebuildRegressionTests
{
    [Fact]
    public void StringVector_RebuildAfterDynamicEdit_DoesNotDuplicateMatchingRow()
    {
        using var scope = new USequenceIntegrationTestHelpers.DeterministicIndexedSequenceEnvironment();
        using var sequence = scope.CreateSequenceWithIndexes();
        sequence.Load(new object[]
        {
            USequenceIntegrationTestHelpers.Row(1, "ALICE", 30, "news"),
            USequenceIntegrationTestHelpers.Row(2, "BOB", 40, "news")
        });
        sequence.Build();
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(1, "ALICIA", 31, "tech"));
        sequence.Build();

        int[] ids = sequence.GetAllByLike(0, "ALI").Cast<object[]>()
            .Select(x => (int)x[0]).ToArray();
        Assert.Equal(new[] { 1 }, ids);
        Assert.Empty(sequence.GetAllByValue(0, "ALICE", _ => Array.Empty<IComparable>()));
    }

    [Fact]
    public void ExternalSortedIndex_RebuildAfterDynamicEdit_DoesNotDuplicateMatchingRow()
    {
        var recordType = USequenceIntegrationTestHelpers.RecordType;
        using var sequence = new USequence(recordType, null,
            () => new MemoryStream(), _ => false, optimise: false);
        sequence.SetPrimaryKey<int>(row => (int)((object[])row)[0]);
        var index = new ESortIndex32(() => new MemoryStream(), sequence,
            row => new[] { (int)((object[])row)[2] }, Comparer<int>.Default);
        sequence.uindexes = new IUIndex[] { index };

        sequence.Load(new object[]
        {
            USequenceIntegrationTestHelpers.Row(1, "ALICE", 30, "news"),
            USequenceIntegrationTestHelpers.Row(2, "BOB", 40, "sports")
        });
        sequence.Build();
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(1, "ALICIA", 55, "tech"));
        sequence.Build();

        int[] ids = index.GetManyByValue(55).Cast<object[]>()
            .Select(x => (int)x[0]).ToArray();
        Assert.Equal(new[] { 1 }, ids);
        Assert.Empty(index.GetManyByValue(30));
    }
}
