using Xunit;

namespace Polar.DB.Tests;

public class USequenceTraversalTests
{
    [Fact]
    public void ElementValues_FiltersOut_Empty_And_Superseded_Records()
    {
        using var env = new USequenceIntegrationTestHelpers.DeterministicIndexedSequenceEnvironment();
        var sequence = env.CreateSequenceWithIndexes(optimise: false);

        sequence.Load(new object[]
        {
            USequenceIntegrationTestHelpers.Row(1, "ALICE", 30, "news"),
            USequenceIntegrationTestHelpers.Row(2, "", 99, "ignored"),
            USequenceIntegrationTestHelpers.Row(3, "BOB", 40, "sports")
        });
        sequence.Build();

        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(3, "BOB-NEW", 41, "news"));
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(4, "", 0, "ignored"));

        var values = sequence.ElementValues().Cast<object[]>().ToArray();

        Assert.Equal(new[] { 1, 3 }, values.Select(r => (int)r[0]).OrderBy(x => x).ToArray());
        Assert.Equal("ALICE", (string)values.Single(r => (int)r[0] == 1)[1]);
        Assert.Equal("BOB-NEW", (string)values.Single(r => (int)r[0] == 3)[1]);
        Assert.DoesNotContain(values, r => string.IsNullOrEmpty((string)r[1]));
    }

    [Fact]
    public void Scan_Visits_Only_Original_NonEmpty_Records_In_Physical_Order()
    {
        using var env = new USequenceIntegrationTestHelpers.DeterministicIndexedSequenceEnvironment();
        var sequence = env.CreateSequenceWithIndexes(optimise: false);

        sequence.Load(new object[]
        {
            USequenceIntegrationTestHelpers.Row(1, "ALICE", 30, "news"),
            USequenceIntegrationTestHelpers.Row(2, "BOB", 40, "sports")
        });
        sequence.Build();

        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(2, "BOB-NEW", 41, "news"));
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(5, "", 0, "ignored"));
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(6, "CLARA", 25, "tech"));

        var visitedIds = new List<int>();
        var visitedNames = new List<string>();
        var visitedOffsets = new List<long>();

        sequence.Scan((off, record) =>
        {
            visitedOffsets.Add(off);
            visitedIds.Add(USequenceIntegrationTestHelpers.IdOf(record));
            visitedNames.Add(USequenceIntegrationTestHelpers.NameOf(record));
            return true;
        });

        Assert.Equal(new[] { 1, 2, 6 }, visitedIds);
        Assert.Equal(new[] { "ALICE", "BOB-NEW", "CLARA" }, visitedNames);
        Assert.Equal(3, visitedOffsets.Count);
        Assert.True(visitedOffsets.SequenceEqual(visitedOffsets.OrderBy(x => x)));
    }

    [Fact]
    public void Traversal_After_Reopen_Remains_Consistent_With_Primary_Key_Filtering()
    {
        using var env = new USequenceIntegrationTestHelpers.DeterministicIndexedSequenceEnvironment();

        var sequence = env.CreateSequenceWithIndexes(optimise: false);
        sequence.Load(new object[]
        {
            USequenceIntegrationTestHelpers.Row(1, "ALICE", 30, "news"),
            USequenceIntegrationTestHelpers.Row(2, "BOB", 40, "sports")
        });
        sequence.Build();

        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(2, "BOB-NEW", 41, "news"));
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(3, "", 0, "ignored"));
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(4, "CLARA", 25, "tech"));
        sequence.Close();

        var reopened = env.CreateSequenceWithIndexes(optimise: false);
        reopened.Refresh();

        var values = reopened.ElementValues().Cast<object[]>().ToArray();

        Assert.Equal(new[] { 1, 2, 4 }, values.Select(r => (int)r[0]).ToArray());
        Assert.Equal("ALICE", (string)values[0][1]);
        Assert.Equal("BOB-NEW", (string)values[1][1]);
        Assert.Equal("CLARA", (string)values[2][1]);
    }
}
