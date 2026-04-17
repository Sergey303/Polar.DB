namespace Polar.DB
{
    /// <summary>
    /// Secondary hash-based index for multi-valued comparable keys.
    /// </summary>
    /// <remarks>
    /// This index stores only hash buckets, so consumers should apply exact key filtering on returned records.
    /// Static state is persisted in sorted hash/offset sequences; appended state is tracked in memory.
    /// </remarks>
    public class UVecIndex : IUIndex
    {
        private readonly USequence sequence;
        private readonly Func<object, IEnumerable<IComparable>> keysFunc;
        private readonly Func<IComparable, int> hashOfKey;
        private readonly UniversalSequenceBase hkeys;
        private readonly UniversalSequenceBase offsets;
        private readonly bool keysinmemory;
        private readonly bool ignorecase;

        private sealed class DynPairsSet
        {
            private int[] hvalues;
            private long[] offsets;
            private readonly USequence sequ;
            private readonly Func<IComparable, int> hashOfKey;

            internal DynPairsSet(USequence sequ, Func<IComparable, int> hashOfKey)
            {
                this.sequ = sequ;
                this.hashOfKey = hashOfKey;
                hvalues = Array.Empty<int>();
                offsets = Array.Empty<long>();
            }

            internal void Clear()
            {
                hvalues = Array.Empty<int>();
                offsets = Array.Empty<long>();
            }

            internal void OnAppendValues(IComparable[] adds, long offset)
            {
                int len = hvalues.Length;
                int nplus = adds.Length;
                if (nplus == 0) return;

                int[] vals = new int[len + nplus];
                long[] offs = new long[len + nplus];
                for (int i = 0; i < len; i++)
                {
                    vals[i] = hvalues[i];
                    offs[i] = offsets[i];
                }
                for (int i = 0; i < nplus; i++)
                {
                    vals[len + i] = hashOfKey(adds[i]);
                    offs[len + i] = offset;
                }

                Array.Sort(vals, offs);
                hvalues = vals;
                offsets = offs;
            }

            internal IEnumerable<ObjOff> GetAllByValue(IComparable valuesample)
            {
                int hashofvaluesample = hashOfKey(valuesample);
                int ind = Array.BinarySearch(hvalues, hashofvaluesample);
                if (ind < 0) yield break;

                yield return new ObjOff(sequ.GetByOffset(offsets[ind]), offsets[ind]);

                int i = ind - 1;
                while (i >= 0)
                {
                    if (hvalues[i] != hashofvaluesample) break;
                    yield return new ObjOff(sequ.GetByOffset(offsets[i]), offsets[i]);
                    i--;
                }

                i = ind + 1;
                while (i < hvalues.Length)
                {
                    if (hvalues[i] != hashofvaluesample) break;
                    yield return new ObjOff(sequ.GetByOffset(offsets[i]), offsets[i]);
                    i++;
                }
            }
        }

        private readonly DynPairsSet dynindex;
        private int[]? hkeys_arr;

        /// <summary>
        /// Creates a multi-valued hash index.
        /// </summary>
        /// <param name="streamGen">Factory for streams used by persisted index parts.</param>
        /// <param name="sequence">Owner sequence whose elements are indexed.</param>
        /// <param name="keysFunc">Extractor returning one or many comparable keys for each sequence element.</param>
        /// <param name="hashOfKey">Hash function used to bucket comparable keys.</param>
        /// <param name="ignorecase">When <see langword="true"/>, string keys are normalized to uppercase before hashing.</param>
        public UVecIndex(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, IEnumerable<IComparable>> keysFunc,
            Func<IComparable, int> hashOfKey,
            bool ignorecase = false)
        {
            this.sequence = sequence;
            this.keysFunc = keysFunc;
            this.hashOfKey = hashOfKey;
            keysinmemory = false;
            this.ignorecase = ignorecase;

            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            dynindex = new DynPairsSet(sequence, hashOfKey);
        }

        /// <summary>
        /// Clears static and dynamic index state.
        /// </summary>
        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            dynindex.Clear();
        }

        /// <summary>
        /// Flushes persisted static index sequences.
        /// </summary>
        public void Flush()
        {
            hkeys.Flush();
            offsets.Flush();
        }

        /// <summary>
        /// Flushes and closes persisted static index sequences.
        /// </summary>
        public void Close()
        {
            hkeys.Close();
            offsets.Close();
        }

        /// <summary>
        /// Reloads persisted static state.
        /// </summary>
        public void Refresh()
        {
            if (keysinmemory)
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            else
                hkeys.Refresh();

            offsets.Refresh();
        }

        /// <summary>
        /// Rebuilds static index state from the owner sequence logical view.
        /// </summary>
        public void Build()
        {
            List<int>? hkeys_list = new List<int>();
            List<long>? offsets_list = new List<long>();
            sequence.Scan((off, obj) =>
            {
                var keys = keysFunc(obj);
                foreach (IComparable key in keys)
                {
                    IComparable k = key;
                    if (ignorecase) k = ((string)k).ToUpper();
                    offsets_list!.Add(off);
                    hkeys_list!.Add(hashOfKey(k));
                }

                return true;
            });

            hkeys_arr = hkeys_list.ToArray();
            long[] offsets_arr = offsets_list.ToArray();

            Array.Sort(hkeys_arr, offsets_arr);

            hkeys.Clear();
            foreach (var hkey in hkeys_arr)
            {
                hkeys.AppendElement(hkey);
            }
            hkeys.Flush();

            if (!keysinmemory)
            {
                hkeys_arr = null;
            }

            offsets.Clear();
            foreach (var off in offsets_arr)
            {
                offsets.AppendElement(off);
            }
            offsets.Flush();
        }

        /// <summary>
        /// Appends extracted keys of one newly appended element to dynamic in-memory state.
        /// </summary>
        /// <param name="element">Appended sequence element.</param>
        /// <param name="offset">Physical stream offset of the appended element.</param>
        public void OnAppendElement(object element, long offset)
        {
            var keys = keysFunc(element)
                .Select(k => ignorecase ? ((string)k).ToUpper() : k)
                .ToArray();

            dynindex.OnAppendValues(keys, offset);
        }

        private long FindFirstStaticIndexByHash(int hkey)
        {
            long count = hkeys.Count();
            if (count == 0) return -1;

            long left = 0;
            long right = count;
            while (left < right)
            {
                long mid = left + (right - left) / 2;
                int midValue = (int)hkeys.GetByIndex(mid);

                if (midValue < hkey)
                    left = mid + 1;
                else
                    right = mid;
            }

            if (left >= count) return -1;
            return (int)hkeys.GetByIndex(left) == hkey ? left : -1;
        }

        private IEnumerable<ObjOff> GetStaticByHash(int hashofvaluesample)
        {
            if (hkeys_arr != null)
            {
                int ind = Array.BinarySearch(hkeys_arr, hashofvaluesample);
                if (ind < 0) yield break;

                while (ind > 0 && hkeys_arr[ind - 1] == hashofvaluesample)
                    ind--;

                while (ind < hkeys_arr.Length && hkeys_arr[ind] == hashofvaluesample)
                {
                    long off = (long)offsets.GetByIndex(ind);
                    yield return new ObjOff(sequence.GetByOffset(off), off);
                    ind++;
                }

                yield break;
            }

            long first = FindFirstStaticIndexByHash(hashofvaluesample);
            if (first < 0) yield break;

            long count = hkeys.Count();
            for (long i = first; i < count; i++)
            {
                int current = (int)hkeys.GetByIndex(i);
                if (current != hashofvaluesample) yield break;

                long off = (long)offsets.GetByIndex(i);
                yield return new ObjOff(sequence.GetByOffset(off), off);
            }
        }

        /// <summary>
        /// Returns all candidate records whose hashed extracted values match <paramref name="valuesample"/>.
        /// </summary>
        /// <param name="valuesample">Lookup value sample.</param>
        /// <returns>Dynamic and static candidates; consumers should apply exact filtering after retrieval.</returns>
        public IEnumerable<ObjOff> GetAllByValue(IComparable valuesample)
        {
            if (ignorecase)
                valuesample = ((string)valuesample).ToUpper();

            int hashofvaluesample = hashOfKey(valuesample);

            foreach (var v in dynindex.GetAllByValue(valuesample))
                yield return v;

            foreach (var v in GetStaticByHash(hashofvaluesample))
                yield return v;
        }
    }
}
