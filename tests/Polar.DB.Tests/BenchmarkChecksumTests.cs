using Xunit;

namespace PolarDbBenchmarks;

public class BenchmarkChecksumTests
{
    [Fact]
    public void HashRowsIgnoresRowOrder()
    {
        var rows = new[]
        {
            new Row(1, "id-000000001", 1, "group-0001", "payload-000000001"),
            new Row(2, "id-000000002", 1, "group-0001", "payload-000000002"),
            new Row(3, "id-000000003", 2, "group-0002", "payload-000000003")
        };

        var reversed = rows.Reverse().ToArray();

        Assert.Equal(BenchmarkChecksum.HashRows(rows), BenchmarkChecksum.HashRows(reversed));
    }

    [Fact]
    public void HashRowsDetectsDifferentRowsWithSameCount()
    {
        var expected = new[]
        {
            new Row(1, "id-000000001", 1, "group-0001", "payload-000000001"),
            new Row(2, "id-000000002", 1, "group-0001", "payload-000000002")
        };

        var actual = new[]
        {
            new Row(1, "id-000000001", 1, "group-0001", "payload-000000001"),
            new Row(3, "id-000000003", 1, "group-0001", "payload-000000003")
        };

        Assert.NotEqual(BenchmarkChecksum.HashRows(expected), BenchmarkChecksum.HashRows(actual));
    }
}
