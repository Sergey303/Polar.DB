using Polar.DB;

namespace Polar.Universal;

internal static class UKeyIndexCompaction
{
    internal static UKeyIndexSnapshot BuildSnapshot(
        USequence sequence,
        Func<object, IComparable> keyFunc,
        Func<IComparable, int> hashOfKey,
        CancellationToken cancellationToken)
    {
        var hashes = new List<int>();
        var offsets = new List<long>();

        sequence.Scan((offset, obj) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            hashes.Add(hashOfKey(keyFunc(obj)));
            offsets.Add(offset);
            return true;
        });

        int[] hashArray = hashes.ToArray();
        long[] offsetArray = offsets.ToArray();
        Array.Sort(hashArray, offsetArray);

        return new UKeyIndexSnapshot(hashArray, offsetArray);
    }

    internal static UKeyIndexSnapshot ReadSnapshot(
        UniversalSequenceBase hashes,
        UniversalSequenceBase offsets)
    {
        return new UKeyIndexSnapshot(
            hashes.ElementValues().Cast<int>().ToArray(),
            offsets.ElementValues().Cast<long>().ToArray());
    }

    internal static void WriteSnapshot(
        UniversalSequenceBase hashes,
        UniversalSequenceBase offsets,
        UKeyIndexSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        hashes.Clear();
        foreach (int hash in snapshot.Hashes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hashes.AppendElement(hash);
        }

        hashes.Flush();
        offsets.Clear();

        foreach (long offset in snapshot.Offsets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets.AppendElement(offset);
        }

        offsets.Flush();
    }
}
