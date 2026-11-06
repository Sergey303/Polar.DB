using PolarDbBenchmarks;
using Xunit;

namespace Polar.DB.Tests;

public class BenchmarkDatasetTests
{
    [Theory]
    [InlineData(ExperimentKind.PkIntLookup)]
    [InlineData(ExperimentKind.ExternalStringLookup)]
    [InlineData(ExperimentKind.ExternalFamousGuidLookup)]
    public void Dataset_ReplaysSameRowsAndSupportsRandomAccess(ExperimentKind kind)
    {
        var dataset = BenchmarkData.Dataset(107, kind, startId: 501);
        var firstPass = dataset.ToArray();

        Assert.Equal(107, dataset.Count);
        Assert.Equal(107, dataset.LongLength);
        Assert.Equal(501, dataset[0].Id);
        Assert.Equal(607, dataset[^1].Id);
        Assert.Equal(firstPass, dataset.ToArray());
        for (var i = 0; i < dataset.Count; i++)
            Assert.Equal(firstPass[i], dataset[i]);

        Assert.Equal(BenchmarkChecksum.HashRows(firstPass), BenchmarkChecksum.HashRows(dataset));
    }

    [Fact]
    public void Dataset_ConstructionHasConstantMemoryCost()
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var dataset = BenchmarkData.Dataset(5_000_000, ExperimentKind.PkStringLookup);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(allocated, 0, 64_000);
        Assert.Equal(5_000_000, dataset.Count);
        Assert.Equal(5_000_000, dataset.LongLength);
        Assert.Equal(5_000_000, dataset[^1].Id);
        GC.KeepAlive(dataset);
    }

    [Fact]
    public void Dataset_EmptyAndOutOfRangeCases()
    {
        var empty = BenchmarkData.Dataset(0, ExperimentKind.PkIntLookup);
        Assert.Empty(empty);
        Assert.Throws<ArgumentOutOfRangeException>(() => empty[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BenchmarkData.Dataset(-1, ExperimentKind.PkIntLookup));

        var dataset = BenchmarkData.Dataset(2, ExperimentKind.PkIntLookup, startId: 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => dataset[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => dataset[2]);
        Assert.Throws<OverflowException>(() =>
            BenchmarkData.Dataset(2, ExperimentKind.PkIntLookup, long.MaxValue)[1]);
    }
}
