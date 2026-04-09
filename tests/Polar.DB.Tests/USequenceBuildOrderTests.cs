using Xunit;

namespace Polar.DB.Tests;

public class USequenceBuildOrderTests
{
    [Fact]
    public void Build_FlushesSequence_BuildsAndPersistsIndexes_SavesState_And_ReopenRemainsConsistent()
    {
        using var env = new USequenceIntegrationTestHelpers.DeterministicIndexedSequenceEnvironment();

        var sequence = env.CreateSequenceWithIndexes(optimise: false);

        sequence.Load(new object[]
        {
            USequenceIntegrationTestHelpers.Row(1, "ALICE", 30, "news", "tech"),
            USequenceIntegrationTestHelpers.Row(2, "BOB", 40, "sports"),
            USequenceIntegrationTestHelpers.Row(3, "CLARA", 30, "news")
        });

        sequence.Build();

        Assert.Equal(new[] { 1, 2, 3 }, sequence.ElementValues().Select(USequenceIntegrationTestHelpers.IdOf).ToArray());

        var state = USequenceIntegrationTestHelpers.ReadStateFile(env.StateFilePath);
        Assert.Equal(USequenceIntegrationTestHelpers.InnerCount(sequence), state.Count);
        Assert.Equal(USequenceIntegrationTestHelpers.InnerAppendOffset(sequence), state.AppendOffset);

        var byKey = Assert.IsType<object[]>(sequence.GetByKey(2));
        Assert.Equal("BOB", (string)byKey[1]);

        var byName = sequence.GetAllByValue(0, "alice", _ => Array.Empty<IComparable>()).Cast<object[]>().ToArray();
        Assert.Single(byName);
        Assert.Equal(1, (int)byName[0][0]);

        var byAge = sequence.GetAllByValue(1, 30, _ => Array.Empty<IComparable>()).Cast<object[]>().ToArray();
        Assert.Equal(new[] { 1, 3 }, byAge.Select(r => (int)r[0]).OrderBy(x => x).ToArray());

        var byTag = sequence.GetAllByValue(2, "NEWS", USequenceIntegrationTestHelpers.TagsOf, ignorecase: true)
            .Cast<object[]>().ToArray();
        Assert.Equal(new[] { 1, 3 }, byTag.Select(r => (int)r[0]).OrderBy(x => x).ToArray());

        var bySample = sequence.GetAllBySample(3, USequenceIntegrationTestHelpers.Row(0, "CLARA", 0))
            .Cast<object[]>().ToArray();
        Assert.Single(bySample);
        Assert.Equal(3, (int)bySample[0][0]);

        sequence.Close();

        var reopened = env.CreateSequenceWithIndexes(optimise: false);
        reopened.Refresh();

        Assert.Equal(new[] { 1, 2, 3 }, reopened.ElementValues().Select(USequenceIntegrationTestHelpers.IdOf).ToArray());

        var reopenedState = USequenceIntegrationTestHelpers.ReadStateFile(env.StateFilePath);
        Assert.Equal(USequenceIntegrationTestHelpers.InnerCount(reopened), reopenedState.Count);
        Assert.Equal(USequenceIntegrationTestHelpers.InnerAppendOffset(reopened), reopenedState.AppendOffset);

        var reopenedByKey = Assert.IsType<object[]>(reopened.GetByKey(2));
        Assert.Equal("BOB", (string)reopenedByKey[1]);

        var reopenedByName = reopened.GetAllByValue(0, "alice", _ => Array.Empty<IComparable>()).Cast<object[]>().ToArray();
        Assert.Single(reopenedByName);
        Assert.Equal(1, (int)reopenedByName[0][0]);

        var reopenedByAge = reopened.GetAllByValue(1, 30, _ => Array.Empty<IComparable>()).Cast<object[]>().ToArray();
        Assert.Equal(new[] { 1, 3 }, reopenedByAge.Select(r => (int)r[0]).OrderBy(x => x).ToArray());

        var reopenedByTag = reopened.GetAllByValue(2, "NEWS", USequenceIntegrationTestHelpers.TagsOf, ignorecase: true)
            .Cast<object[]>().ToArray();
        Assert.Equal(new[] { 1, 3 }, reopenedByTag.Select(r => (int)r[0]).OrderBy(x => x).ToArray());

        var reopenedBySample = reopened.GetAllBySample(3, USequenceIntegrationTestHelpers.Row(0, "CLARA", 0))
            .Cast<object[]>().ToArray();
        Assert.Single(reopenedBySample);
        Assert.Equal(3, (int)reopenedBySample[0][0]);
    }
}
