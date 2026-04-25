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
    public class UKeyIndex
    {
        private readonly USequence sequence;
        private readonly Func<object, IComparable> keyFunc;
        private readonly Func<IComparable, int> hashOfKey;
        private readonly UniversalSequenceBase hkeys;
        private readonly UniversalSequenceBase offsets;
        private readonly Dictionary<IComparable, long> keyoff_dic;
        private readonly bool keysinmemory;
        private int[]? hkeys_arr;
        private long[]? offsets_arr;

        public UKeyIndex(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, IComparable> keyFunc,
            Func<IComparable, int> hashOfKey,
            bool keysinmemory = true)
        {
            _ = streamGen ?? throw new ArgumentNullException(nameof(streamGen));
            this.sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            this.keyFunc = keyFunc ?? throw new ArgumentNullException(nameof(keyFunc));
            this.hashOfKey = hashOfKey ?? throw new ArgumentNullException(nameof(hashOfKey));
            this.keysinmemory = keysinmemory;

            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            keyoff_dic = new Dictionary<IComparable, long>();
        }

        public void OnAppendElement(object element, long offset)
        {
            _ = element ?? throw new ArgumentNullException(nameof(element));
            var key = keyFunc(element);
            keyoff_dic[key] = offset;
        }

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets_arr = null;
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
                offsets_arr = offsets.ElementValues().Cast<long>().ToArray();
            }
            else
            {
                hkeys_arr = null;
                offsets_arr = null;
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
            long[] localOffsets = new long[count];

            for (int i = 0; i < count; i++)
            {
                var entry = snapshot[i];
                localOffsets[i] = entry.Offset;
                localHkeys[i] = hashOfKey(keyFunc(entry.Element));
            }

            hkeys_arr = localHkeys;
            Array.Sort(hkeys_arr, localOffsets);

            offsets_arr = keysinmemory ? localOffsets : null;

            hkeys.Clear();
            hkeys.AppendElements(hkeys_arr.Select(static x => (object)x));
            hkeys.Flush();

            if (!keysinmemory)
            {
                hkeys_arr = null;
                offsets_arr = null;
            }

            offsets.Clear();
            offsets.AppendElements(localOffsets.Select(static x => (object)x));
            offsets.Flush();
        }

public object? GetByKey(IComparable keysample)
{
    _ = keysample ?? throw new ArgumentNullException(nameof(keysample));

    if (keyoff_dic.TryGetValue(keysample, out long off))
        return sequence.GetByOffset(off);

    int hkey = hashOfKey(keysample);

    if (hkeys_arr != null)
    {
        int pos = Array.BinarySearch(hkeys_arr, hkey);
        if (pos < 0) return null;

        long[]? localOffsets = offsets_arr;

        long ReadOffset(int index)
        {
            return localOffsets != null
                ? localOffsets[index]
                : (long)offsets.GetByIndex(index);
        }

        object? ReadValueAt(int index)
        {
            long offset = ReadOffset(index);
            return sequence.GetByOffset(offset);
        }

        object? val = ReadValueAt(pos);
        if (val == null) return null;

        var key = keyFunc(val);
        if (key.CompareTo(keysample) == 0) return val;

        int left = pos - 1;
        int right = pos + 1;

        while (left >= 0 || right < hkeys_arr.Length)
        {
            bool checkedAny = false;

            if (left >= 0 && hkeys_arr[left] == hkey)
            {
                checkedAny = true;

                val = ReadValueAt(left);
                if (val == null) return null;

                key = keyFunc(val);
                if (key.CompareTo(keysample) == 0) return val;

                left--;
            }
            else
            {
                left = -1;
            }

            if (right < hkeys_arr.Length && hkeys_arr[right] == hkey)
            {
                checkedAny = true;

                val = ReadValueAt(right);
                if (val == null) return null;

                key = keyFunc(val);
                if (key.CompareTo(keysample) == 0) return val;

                right++;
            }
            else
            {
                right = hkeys_arr.Length;
            }

            if (!checkedAny) break;
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

        var key = keyFunc(val);
        if (hashOfKey(key) != hkey) break;
        if (key.CompareTo(keysample) == 0) return val;
    }

    return null;
}

        /// <summary>
        /// Метод находит номер первого элемента в таблице хеш-значений, имеющего заданный хеш
        /// </summary>
        /// <param name="hkey"></param>
        /// <returns></returns>
        private long GetFirstNom(int hkey)
        {
            long start = 0, end = hkeys.Count() - 1, right_equal = -1;
            // Сжимаем диапазон
            while (end - start > 1)
            {
                // Находим середину
                long middle = (start + end) / 2;
                int middle_value = (int)hkeys.GetByIndex(middle);
                if (middle_value < hkey)
                {  // Займемся правым интервалом
                    start = middle;
                }
                else if (middle_value > hkey)
                {  // Займемся левым интервалом
                    end = middle;
                }
                else
                {  // Середина дает РАВНО
                    end = middle;
                    right_equal = middle;
                }
            }
            return right_equal;
        }

        public bool IsOriginal(IComparable key, long offset)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            if (keyoff_dic.TryGetValue(key, out long off))
                return off == offset;

            return true;
        }
    }
}