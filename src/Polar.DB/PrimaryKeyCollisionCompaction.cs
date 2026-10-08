namespace Polar.Universal;

internal static class PrimaryKeyCollisionCompaction
{
    internal static int Compact<TKey>(
        USequence sequence,
        BuildEntry[] entries,
        int start,
        int count,
        int destinationIndex,
        List<long> staleOffsets,
        Func<object, TKey> keySelector,
        Comparison<TKey> keyComparison)
    {
        var collisions = new CollisionEntry<TKey>[count];
        for (var i = 0; i < count; i++)
        {
            var entry = entries[start + i];
            var value = sequence.GetByOffset(entry.Offset);
            collisions[i] = new CollisionEntry<TKey>(keySelector(value), entry);
        }

        Array.Sort(collisions, new CollisionEntryComparer<TKey>(keyComparison));

        var index = 0;
        while (index < collisions.Length)
        {
            var groupStart = index;
            var latest = collisions[index++];

            while (index < collisions.Length &&
                   keyComparison(latest.Key, collisions[index].Key) == 0)
            {
                latest = collisions[index++];
            }

            for (var i = groupStart; i < index - 1; i++)
                staleOffsets.Add(collisions[i].Entry.Offset);

            if (latest.Entry.IsEmpty)
                staleOffsets.Add(latest.Entry.Offset);
            else
                entries[destinationIndex++] = latest.Entry;
        }

        return destinationIndex;
    }

    private readonly struct CollisionEntry<TKey>
    {
        internal CollisionEntry(TKey key, BuildEntry entry)
        {
            Key = key;
            Entry = entry;
        }

        internal TKey Key { get; }
        internal BuildEntry Entry { get; }
    }

    private sealed class CollisionEntryComparer<TKey> : IComparer<CollisionEntry<TKey>>
    {
        private readonly Comparison<TKey> _keyComparison;

        internal CollisionEntryComparer(Comparison<TKey> keyComparison)
        {
            _keyComparison = keyComparison;
        }

        public int Compare(CollisionEntry<TKey> left, CollisionEntry<TKey> right)
        {
            var keyResult = _keyComparison(left.Key, right.Key);
            return keyResult != 0
                ? keyResult
                : left.Entry.Offset.CompareTo(right.Entry.Offset);
        }
    }
}
