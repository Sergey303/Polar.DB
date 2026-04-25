// UKeyIndex.cs

namespace Polar.DB
{
    /// <summary>
    /// Primary-key index used by <see cref="USequence"/>.
    /// </summary>
    /// <remarks>
    /// The index combines a persisted static hash/offset part with a dynamic in-memory map from key to latest offset.
    /// Originality checks use only dynamic state: if a key exists in the map, only that offset is considered current.
    /// </remarks>
    internal class UKeyIndex
    {
        private readonly USequence sequence;
        private readonly Func<object, IComparable> keyFunc;
        private readonly Func<IComparable, int> hashOfKey;
        private readonly UniversalSequenceBase hkeys;
        private readonly UniversalSequenceBase offsets;
        private readonly Dictionary<IComparable, long> keyoff_dic;
        private readonly bool keysinmemory;
        private int[]? hkeys_arr;

        public UKeyIndex(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, IComparable> keyFunc,
            Func<IComparable, int> hashOfKey,
            bool keysinmemory = true)
        {
            this.sequence = sequence;
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            this.keysinmemory = keysinmemory;

            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            keyoff_dic = new Dictionary<IComparable, long>();
        }

        public void OnAppendElement(object element, long offset)
        {
            var key = keyFunc(element);
            keyoff_dic[key] = offset;
        }

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            keyoff_dic.Clear();
        }

        public void Flush()
        {
            hkeys.Flush();
            offsets.Flush();
        }

        public void Close()
        {
            hkeys.Close();
            offsets.Close();
        }

        /// <summary>
        /// Reloads persisted static state.
        /// </summary>
        /// <remarks>
        /// Dynamic key map is intentionally preserved because it tracks the append tail above the synchronized state point.
        /// </remarks>
        public void Refresh()
        {
            if (keysinmemory)
            {
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            }
            else
            {
                hkeys_arr = null;
                hkeys.Refresh();
            }

            offsets.Refresh();
        }

        public void Build()
        {
            BuildFromSnapshot(sequence.CreateLogicalBuildSnapshot());
        }

        internal void BuildFromSnapshot(IReadOnlyList<USequence.LogicalBuildEntry> snapshot)
        {
            int count = snapshot.Count;
            int[] localHkeys = new int[count];
            long[] offsets_arr = new long[count];
            for (int i = 0; i < count; i++)
            {
                var entry = snapshot[i];
                offsets_arr[i] = entry.Offset;
                localHkeys[i] = hashOfKey(keyFunc(entry.Element));
            }

            hkeys_arr = localHkeys;
            Array.Sort(hkeys_arr, offsets_arr);

            hkeys.Clear();
            hkeys.AppendElements(hkeys_arr.Select(static x => (object)x));
            hkeys.Flush();

            if (!keysinmemory)
            {
                hkeys_arr = null;
            }

            offsets.Clear();
            offsets.AppendElements(offsets_arr.Select(static x => (object)x));
            offsets.Flush();
        }

        public object? GetByKey(IComparable keysample)
        {
            if (keyoff_dic.TryGetValue(keysample, out long off))
            {
                object? vv = sequence.GetByOffset(off);
                return vv;
            }

            int hkey = hashOfKey(keysample);

            if (hkeys_arr != null)
            {
                int pos = Array.BinarySearch(hkeys_arr, hkey);
                if (pos < 0) return null;

                int p = pos;
                while (p >= 0 && hkeys_arr[p] == hkey)
                {
                    pos = p;
                    p--;
                }

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);
                    object? val = sequence.GetByOffset(offset);
                    if (val == null) return null;
                    var k = keyFunc(val);
                    if (k.CompareTo(keysample) == 0) return val;
                    pos++;
                }

                return null;
            }

            long first = GetFirstNom(hkey);
            if (first == -1) return null;

            for (long nom = first; nom < hkeys.Count(); nom++)
            {
                long offset = (long)offsets.GetByIndex(nom);
                object? val = sequence.GetByOffset(offset);
                if (val == null) break;
                var k = keyFunc(val);
                if (hashOfKey(k) != hkey) break;
                if (k.CompareTo(keysample) == 0) return val;
            }

            return null;
        }

        private long GetFirstNom(int hkey)
        {
            long count = hkeys.Count();
            if (count == 0) return -1;

            long left = 0;
            long right = count;
            while (left < right)
            {
                long middle = left + (right - left) / 2;
                int middleValue = (int)hkeys.GetByIndex(middle);

                if (middleValue < hkey)
                    left = middle + 1;
                else
                    right = middle;
            }

            if (left >= count) return -1;
            return (int)hkeys.GetByIndex(left) == hkey ? left : -1;
        }

        public bool IsOriginal(IComparable key, long offset)
        {
            if (keyoff_dic.TryGetValue(key, out long off))
                return off == offset;

            return true;
        }
    }
}
