using Polar.Universal;

namespace Polar.DB.ExternalKey;

internal static class ExternalKeyIndexCompaction<T>
    where T : IComparable<T>
{
    internal static ExternalKeyIndexSnapshot<T> BuildSnapshot(
        USequence sequence,
        Func<object, IEnumerable<T>> keysFunc,
        IComparer<T> comparer,
        CancellationToken cancellationToken)
    {
        var keys = new List<T>();
        var offsets = new List<long>();

        sequence.Scan((offset, obj) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var key in keysFunc(obj) ?? Enumerable.Empty<T>())
            {
                keys.Add(key);
                offsets.Add(offset);
            }

            return true;
        });

        T[] keysArray = keys.ToArray();
        long[] offsetsArray = offsets.ToArray();
        Array.Sort(keysArray, offsetsArray, comparer);

        return new ExternalKeyIndexSnapshot<T>(keysArray, offsetsArray);
    }

    internal static ExternalKeyIndexSnapshot<T> ReadSnapshot(
        UniversalSequenceBase keys,
        UniversalSequenceBase offsets)
    {
        T[] keysArray = keys.ElementValues()
            .Select(ExternalKeyIndexKeyCodec<T>.FromStorage)
            .ToArray();

        long[] offsetsArray = offsets.ElementValues()
            .Cast<long>()
            .ToArray();

        return new ExternalKeyIndexSnapshot<T>(keysArray, offsetsArray);
    }

    internal static void WriteSnapshot(
        UniversalSequenceBase keys,
        UniversalSequenceBase offsets,
        ExternalKeyIndexSnapshot<T> snapshot,
        CancellationToken cancellationToken)
    {
        keys.Clear();

        foreach (var key in snapshot.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            keys.AppendElement(ExternalKeyIndexKeyCodec<T>.ToStorage(key));
        }

        keys.Flush();
        offsets.Clear();

        foreach (var offset in snapshot.Offsets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets.AppendElement(offset);
        }

        offsets.Flush();
    }
}
