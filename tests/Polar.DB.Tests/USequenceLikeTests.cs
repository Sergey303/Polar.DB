using Xunit;

namespace Polar.DB.Tests;

public class USequenceLikeTests
{
    [Fact]
    public void GetAllByLike_Returns_Expected_Matches_Excludes_Superseded_And_Empty_Records()
    {
        using var env = new USequenceIntegrationTestHelpers.DeterministicIndexedSequenceEnvironment();
        var sequence = env.CreateSequenceWithIndexes(optimise: false);

        sequence.Load(new object[]
        {
            USequenceIntegrationTestHelpers.Row(10, "ALPHA", 30, "news"),
            USequenceIntegrationTestHelpers.Row(11, "ALBERT", 31, "news"),
            USequenceIntegrationTestHelpers.Row(20, "BOB", 40, "sports")
        });
        sequence.Build();

        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(10, "ALPHA2", 35, "tech"));
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(30, string.Empty, 0, "ignored"));
        sequence.AppendElement(USequenceIntegrationTestHelpers.Row(40, "ALASKA", 22, "travel"));

        var matches = sequence.GetAllByLike(0, "AL").Cast<object[]>().ToArray();

        var ids = matches.Select(r => (int)r[0]).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 10, 11, 40 }, ids);

        Assert.Equal("ALPHA2", (string)matches.Single(r => (int)r[0] == 10)[1]);
        Assert.DoesNotContain(matches, r => (int)r[0] == 10 && (string)r[1] == "ALPHA");
        Assert.DoesNotContain(matches, r => string.IsNullOrEmpty((string)r[1]));
    }
}
