namespace PolarDbBenchmarks;

internal static class PolarMaterializer
{
    public static Row[] ReadAll(string dir, ExperimentKind kind)
    {
        var store = PolarStoreFactory.Open(dir, kind);
        store.Sequence.Refresh();

        try
        {
            return ReadAll(store);
        }
        finally
        {
            store.Sequence.Close();
        }
    }

    public static Row[] ReadAll(PolarStore store) =>
        store.Sequence.ElementValues()
            .Select(PolarRows.FromPolar)
            .OrderBy(row => row.Id)
            .ToArray();
}
