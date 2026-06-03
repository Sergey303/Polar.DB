namespace PolarDbBenchmarks;

public class BenchmarkChecksumTests
{
    [Fact]
    public void HashRowsIgnoresRowOrder()
    {
        var rows = new[]
        {
            Row(1),
            Row(2),
            Row(3)
        };

        var reversed = rows.Reverse().ToArray();

        Assert.Equal(BenchmarkChecksum.HashRows(rows), BenchmarkChecksum.HashRows(reversed));
    }

    [Fact]
    public void HashRowsDetectsDifferentRowsWithSameCount()
    {
        var expected = new[]
        {
            Row(1),
            Row(2)
        };

        var actual = new[]
        {
            Row(1),
            Row(3)
        };

        Assert.NotEqual(BenchmarkChecksum.HashRows(expected), BenchmarkChecksum.HashRows(actual));
    }

    private static Row Row(long id) =>
        new(id, 9_000_000_000L + id, BenchmarkFamousKeys.GuidFor(id), $"id-{id:000000000}",
            (int)(id % 1000), 80_000_000_000L + id % 1000,
            BenchmarkFamousKeys.GuidFor(2_000_000L + id % 1000),
            $"group-{id % 1000:0000}", $"payload-{id:000000000}");
}
