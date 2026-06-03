namespace PolarDbBenchmarks;

public class BenchmarkChecksumTests
{
    [Fact]
    public void HashRowsIgnoresRowOrder()
    {
        var rows = new[]
        {
            Row(1, 1, "payload-000000001"),
            Row(2, 1, "payload-000000002"),
            Row(3, 2, "payload-000000003")
        };

        var reversed = rows.Reverse().ToArray();

        Assert.Equal(BenchmarkChecksum.HashRows(rows), BenchmarkChecksum.HashRows(reversed));
    }

    [Fact]
    public void HashRowsDetectsDifferentRowsWithSameCount()
    {
        var expected = new[]
        {
            Row(1, 1, "payload-000000001"),
            Row(2, 1, "payload-000000002")
        };

        var actual = new[]
        {
            Row(1, 1, "payload-000000001"),
            Row(3, 1, "payload-000000003")
        };

        Assert.NotEqual(BenchmarkChecksum.HashRows(expected), BenchmarkChecksum.HashRows(actual));
    }

    private static Row Row(long id, int externalId, string payload) =>
        new(id, 9_000_000_000L + id, $"guid-{id}", $"id-{id:000000000}",
            externalId, $"group-{externalId:0000}", payload);
}
