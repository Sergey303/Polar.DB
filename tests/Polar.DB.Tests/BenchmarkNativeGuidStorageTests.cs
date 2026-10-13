using PolarDbBenchmarks;
using Xunit;

namespace Polar.DB.Tests;

public class BenchmarkNativeGuidStorageTests
{
    private static readonly Row Sample = new(
        17L,
        9_000_000_017L,
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        "id-000000017",
        17,
        8_000_000_017L,
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        "external-17",
        "payload-17");

    [Fact]
    public void PolarRows_Stores_Guid_Values_Directly()
    {
        object[] storage = Assert.IsType<object[]>(PolarRows.ToPolar(Sample));

        Assert.Equal(10, storage.Length);
        Assert.Equal(Sample.GuidKey, Assert.IsType<Guid>(storage[2]));
        Assert.Equal(Sample.ExternalGuid, Assert.IsType<Guid>(storage[6]));
        Assert.Equal(Sample, PolarRows.FromPolar(storage));
    }

    [Fact]
    public void PolarStoreFactory_PrimaryGuidLookup_Uses_Native_Record_Field()
    {
        string dir = CreateDir();
        try
        {
            PolarStore store = PolarStoreFactory.Open(dir, ExperimentKind.PkGuidLookup);
            try
            {
                store.Sequence.Load(new[] { PolarRows.ToPolar(Sample) });
                store.Sequence.Build();

                object? found = store.Sequence.GetByKey(Sample.GuidKey);
                Assert.NotNull(found);
                Assert.Equal(Sample, PolarRows.FromPolar(found!));
            }
            finally
            {
                store.Sequence.Dispose();
            }
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    public void PolarStoreFactory_ExternalGuidLookup_Uses_Native_Guid_Index()
    {
        string dir = CreateDir();
        try
        {
            PolarStore store = PolarStoreFactory.Open(dir, ExperimentKind.ExternalGuidLookup);
            try
            {
                store.Sequence.Load(new[] { PolarRows.ToPolar(Sample) });
                store.Sequence.Build();

                object found = Assert.Single(store.GuidIndex!.GetManyByValue(Sample.ExternalGuid));
                Assert.Equal(Sample, PolarRows.FromPolar(found));
            }
            finally
            {
                store.Sequence.Dispose();
            }
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static string CreateDir()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "PolarDbTests",
            "benchmark-native-guid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
