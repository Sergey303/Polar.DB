namespace Polar.DB
{
    internal struct HKeyObjOff
    {
        public int hkey;
        public object? obj;
        public long off;
    }

    /// <summary>
    /// Secondary index over full element values with optional hash pre-bucketing.
    /// </summary>
    /// <remarks>
    /// Persisted static state is rebuilt by <see cref="Build()"/>.
    /// <see cref="OnAppendElement(object,long)"/> updates only the dynamic in-memory set.
    /// </remarks>
    public class UIndex : IUIndex
    {
        private readonly USequence sequence;
        private readonly Func<object, bool> applicable;
        private readonly Func<object, int>? hashFunc;
        private readonly Comparer<object> comp;

        private readonly UniversalSequenceBase? hkeys;
        private readonly UniversalSequenceBase offsets;

        private HKeyObjOff[] dynset;
        private readonly Comparer<HKeyObjOff> complex_comp;
        private int[]? hkeys_arr;
        private readonly struct ValueOffset
        {
            internal ValueOffset(object value, long offset)
            {
                Value = value;
                Offset = offset;
            }

            internal object Value { get; }
            internal long Offset { get; }
        }

        /// <summary>
        /// Creates a value index.
        /// </summary>
        /// <param name="streamGen">Factory for streams used by persisted index parts.</param>
        /// <param name="sequence">Owner sequence whose elements are indexed.</param>
        /// <param name="applicable">Predicate that selects elements to include in the index.</param>
        /// <param name="hashFunc">Optional hash selector used to reduce compare range; pass <see langword="null"/> for pure value ordering.</param>
        /// <param name="comp">Comparer used for exact element ordering and equality checks.</param>
        public UIndex(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, bool> applicable,
            Func<object, int>? hashFunc,
            Comparer<object> comp)
        {
            this.sequence = sequence;
            this.applicable = applicable;
            this.hashFunc = hashFunc;
            this.comp = comp;

            if (hashFunc != null)
                hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());

            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            complex_comp = Comparer<HKeyObjOff>.Create((h1, h2) =>
            {
                if (hashFunc != null)
                {
                    int cmp = h1.hkey.CompareTo(h2.hkey);
                    if (cmp != 0) return cmp;
                }

                return comp.Compare(h1.obj!, h2.obj!);
            });

            dynset = Array.Empty<HKeyObjOff>();
        }

        /// <summary>
        /// Clears static and dynamic index state.
        /// </summary>
        public void Clear()
        {
            if (hashFunc != null)
                hkeys!.Clear();

            hkeys_arr = null;
            offsets.Clear();
            dynset = Array.Empty<HKeyObjOff>();
        }

        /// <summary>
        /// Flushes persisted static index sequences.
        /// </summary>
        public void Flush()
        {
            if (hashFunc != null)
                hkeys!.Flush();

            offsets.Flush();
        }

        /// <summary>
        /// Flushes and closes persisted static index sequences.
        /// </summary>
        public void Close()
        {
            if (hashFunc != null)
                hkeys!.Close();

            offsets.Close();
        }

        /// <summary>
        /// Reloads persisted static index state.
        /// </summary>
        public void Refresh()
        {
            if (hashFunc != null)
                hkeys_arr = hkeys!.ElementValues().Cast<int>().ToArray();

            offsets.Refresh();
        }

        /// <summary>
        /// Rebuilds static index state from the owner sequence logical view.
        /// </summary>
        public void Build()
        {
            Build(sequence.CreateLogicalBuildSnapshot());
        }

        internal void Build(IReadOnlyList<USequence.LogicalBuildEntry> snapshot)
        {
            if (hashFunc == null)
                BuildOffsets(snapshot);
            else
                BuildHkeyOffsets(snapshot);
        }

        private void BuildOffsets(IReadOnlyList<USequence.LogicalBuildEntry> snapshot)
        {
            int initialCapacity = snapshot.Count;
            List<ValueOffset> valuesWithOffsets = initialCapacity > 0
                ? new List<ValueOffset>(initialCapacity)
                : new List<ValueOffset>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                if (applicable(entry.Element))
                    valuesWithOffsets.Add(new ValueOffset(entry.Element, entry.Offset));
            }

            ValueOffset[] valuesWithOffsetsArr = valuesWithOffsets.ToArray();
            Array.Sort(valuesWithOffsetsArr, Comparer<ValueOffset>.Create((v1, v2) => comp.Compare(v1.Value, v2.Value)));

            long[] offsets_arr = new long[valuesWithOffsetsArr.Length];
            for (int i = 0; i < valuesWithOffsetsArr.Length; i++)
                offsets_arr[i] = valuesWithOffsetsArr[i].Offset;

            offsets.Clear();
            offsets.AppendElements(offsets_arr.Select(static x => (object)x));
            offsets.Flush();
        }

        private void BuildHkeyOffsets(IReadOnlyList<USequence.LogicalBuildEntry> snapshot)
        {
            int initialCapacity = snapshot.Count;
            List<int> hkeys_list = initialCapacity > 0 ? new List<int>(initialCapacity) : new List<int>();
            List<long> offsets_list = initialCapacity > 0 ? new List<long>(initialCapacity) : new List<long>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                offsets_list.Add(entry.Offset);
                hkeys_list.Add(hashFunc!(entry.Element));
            }

            hkeys_arr = hkeys_list.ToArray();
            long[] offsets_arr = offsets_list.ToArray();

            Array.Sort(hkeys_arr, offsets_arr);

            hkeys!.Clear();
            hkeys.AppendElements(hkeys_arr.Select(static x => (object)x));
            hkeys.Flush();

            offsets.Clear();
            offsets.AppendElements(offsets_arr.Select(static x => (object)x));
            offsets.Flush();
        }

        internal IEnumerable<ObjOff> GetAllBySample(object sample)
        {
            if (dynset.Length > 0)
            {
                HKeyObjOff complex_sample = new HKeyObjOff { obj = sample };
                if (hashFunc != null)
                    complex_sample.hkey = hashFunc(sample);

                var query = dynset.Where(hoo => complex_comp.Compare(hoo, complex_sample) == 0)
                    .Select(hoo => new ObjOff(hoo.obj, hoo.off));
                foreach (var oo in query)
                    yield return oo;
            }

            long first = GetFirstNomOffsets(sample, comp);
            if (first < 0)
                yield break;

            for (long ii = first; ii < offsets.Count(); ii++)
            {
                long off = (long)offsets.GetByIndex(ii);
                object? value = sequence.GetByOffset(off);
                if (comp.Compare(value!, sample) == 0)
                    yield return new ObjOff(value, off);
                else
                    break;
            }
        }

        internal IEnumerable<ObjOff> GetAllByLike(object sample, Comparer<object> comp_like)
        {
            if (dynset.Length > 0)
            {
                foreach (var oo in dynset.Select(hoo => new ObjOff(hoo.obj, hoo.off)))
                {
                    if (comp_like.Compare(oo.obj!, sample) == 0)
                        yield return oo;
                }
            }

            long first = GetFirstNomOffsets(sample, comp_like);
            if (first < 0)
                yield break;

            for (long ii = first; ii < offsets.Count(); ii++)
            {
                long off = (long)offsets.GetByIndex(ii);
                object? value = sequence.GetByOffset(off);
                if (comp_like.Compare(value!, sample) == 0)
                    yield return new ObjOff(value, off);
                else
                    break;
            }
        }

        private int LowerBound(HKeyObjOff[] arr, HKeyObjOff item)
        {
            int lo = 0;
            int hi = arr.Length;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (complex_comp.Compare(arr[mid], item) < 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// Appends one newly added sequence element into dynamic in-memory index state.
        /// </summary>
        /// <param name="element">Appended sequence element.</param>
        /// <param name="offset">Physical stream offset of the appended element.</param>
        public void OnAppendElement(object element, long offset)
        {
            if (!applicable(element)) return;

            var item = new HKeyObjOff
            {
                obj = element,
                off = offset,
                hkey = hashFunc != null ? hashFunc(element) : 0
            };

            int pos = LowerBound(dynset, item);
            var next = new HKeyObjOff[dynset.Length + 1];
            Array.Copy(dynset, 0, next, 0, pos);
            next[pos] = item;
            Array.Copy(dynset, pos, next, pos + 1, dynset.Length - pos);
            dynset = next;
        }

        private long GetFirstNomOffsets(object sample, Comparer<object> comparer)
        {
            long count = offsets.Count();
            if (count == 0) return -1;

            long start = 0;
            long end = offsets.Count() - 1;
            long right_equal = -1;
            int cmp = 0;
            object? middle_value = null;

            while (end - start > 1)
            {
                long middle = (start + end) / 2;
                middle_value = sequence.GetByOffset((long)offsets.GetByIndex(middle));
                cmp = comparer.Compare(middle_value!, sample);
                if (cmp < 0)
                    start = middle;
                else if (cmp > 0)
                    end = middle;
                else
                {
                    end = middle;
                    right_equal = middle;
                }
            }

            if (right_equal == -1)
            {
                long another = cmp < 0 ? end : start;
                middle_value = sequence.GetByOffset((long)offsets.GetByIndex(another));
                cmp = comparer.Compare(middle_value!, sample);
                if (cmp == 0) return another;
            }

            return right_equal;
        }

        private long GetFirstNom(int hkey)
        {
            long start = 0;
            long end = hkeys!.Count() - 1;
            long right_equal = -1;
            while (end - start > 1)
            {
                long middle = (start + end) / 2;
                int middle_value = (int)hkeys.GetByIndex(middle);
                if (middle_value < hkey)
                    start = middle;
                else if (middle_value > hkey)
                    end = middle;
                else
                {
                    end = middle;
                    right_equal = middle;
                }
            }

            return right_equal;
        }
    }
}
