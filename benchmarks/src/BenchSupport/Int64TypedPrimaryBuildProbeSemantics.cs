namespace PolarDbBenchmarks;

internal static class Int64TypedPrimaryBuildProbeSemantics
{
    public static void VerifyDuplicateKeys(string dir)
    {
        Directory.CreateDirectory(dir);
        var store = PolarStoreFactory.Open(dir, ExperimentKind.BuildPrimaryIntOnly);
        try
        {
            long[] values = { 11, 22, 11, 33, 22 };
            var entries = Int64TypedPrimaryBuildProbe.Load(store.Sequence, values);
            Int64TypedPrimaryBuildProbe.Build(store.Sequence, entries);

            var materialized = store.Sequence.ElementValues().Cast<long>().ToArray();
            long[] expected = { 11, 33, 22 };
            if (!materialized.SequenceEqual(expected))
                throw new InvalidDataException(
                    "Typed Int64 primary build probe did not preserve latest physical duplicate-key entries.");

            foreach (var key in expected)
            {
                var value = store.Sequence.GetByKey(key);
                if (value is not long actual || actual != key)
                    throw new InvalidDataException(
                        $"Typed Int64 primary build probe duplicate-key lookup failed for key {key}.");
            }
        }
        finally
        {
            store.Sequence.Close();
        }
    }
}
