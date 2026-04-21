namespace Polar.DB
{
    /// <summary>
    /// Secondary index for multi-valued comparable keys extracted from sequence elements.
    /// </summary>
    /// <remarks>
    /// The index stores a sorted static part in two aligned sequences (<c>values</c> and <c>element_offsets</c>) and
    /// a dynamic in-memory dictionary for values appended after the last build/refresh cycle.
    /// </remarks>
    public class UVectorIndex : IUIndex
    {
        private readonly USequence sequence;
        private readonly Func<object, IEnumerable<IComparable>> valuesFunc;
        private readonly UniversalSequenceBase values;
        private readonly UniversalSequenceBase element_offsets;
        private Dictionary<IComparable, long[]> valueoffs_dic;

        /// <summary>
        /// Creates a vector index.
        /// </summary>
        /// <param name="streamGen">Factory for streams used by persisted index parts.</param>
        /// <param name="sequence">Owner sequence whose elements are indexed.</param>
        /// <param name="tp_value">Schema type of extracted values.</param>
        /// <param name="valuesFunc">Extractor returning one or many comparable values for each sequence element.</param>
        public UVectorIndex(
            Func<Stream> streamGen,
            USequence sequence,
            PType tp_value,
            Func<object, IEnumerable<IComparable>> valuesFunc)
        {
            _ = streamGen ?? throw new ArgumentNullException(nameof(streamGen));
            this.sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _ = tp_value ?? throw new ArgumentNullException(nameof(tp_value));
            this.valuesFunc = valuesFunc ?? throw new ArgumentNullException(nameof(valuesFunc));

            values = new UniversalSequenceBase(tp_value, streamGen());
            element_offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            valueoffs_dic = new Dictionary<IComparable, long[]>();
        }

        /// <summary>
        /// Appends extracted values of one newly appended element to the dynamic in-memory state.
        /// </summary>
        /// <param name="element">Appended sequence element.</param>
        /// <param name="offset">Physical stream offset of the appended element.</param>
        public void OnAppendElement(object element, long offset)
        {
            _ = element ?? throw new ArgumentNullException(nameof(element));
            var vals = valuesFunc(element);
            foreach (var value in vals)
            {
                IComparable key = value;
                if (valueoffs_dic.TryGetValue(key, out var offsets))
                {
                    valueoffs_dic[key] = offsets!.Append(offset).ToArray();
                }
                else
                {
                    valueoffs_dic.Add(key, new[] { offset });
                }
            }
        }

        private IComparable[]? values_arr;

        /// <summary>
        /// Clears static and dynamic index state.
        /// </summary>
        public void Clear()
        {
            values.Clear();
            element_offsets.Clear();
            values_arr = Array.Empty<IComparable>();
            valueoffs_dic = new Dictionary<IComparable, long[]>();
        }

        /// <summary>
        /// Flushes persisted static index sequences.
        /// </summary>
        public void Flush()
        {
            values.Flush();
            element_offsets.Flush();
        }

        /// <summary>
        /// Flushes and closes persisted static index sequences.
        /// </summary>
        public void Close()
        {
            values.Close();
            element_offsets.Close();
        }

        /// <summary>
        /// Reloads persisted static values into memory and refreshes aligned offsets.
        /// </summary>
        public void Refresh()
        {
            values_arr = values.ElementValues().Cast<IComparable>().ToArray();
            element_offsets.Refresh();
        }

        /// <summary>
        /// Rebuilds static index state from the owner sequence logical view.
        /// </summary>
        public void Build()
        {
            List<IComparable> values_list = new List<IComparable>();
            List<long> offsets_list = new List<long>();
            sequence.Scan((off, obj) =>
            {
                var vals = valuesFunc(obj);
                foreach (var v in vals)
                {
                    offsets_list.Add(off);
                    values_list.Add(v);
                }

                return true;
            });

            values_arr = values_list.ToArray();
            long[] offsets_arr = offsets_list.ToArray();

            Array.Sort(values_arr, offsets_arr);

            values.Clear();
            foreach (var v in values_arr)
            {
                values.AppendElement(v);
            }
            values.Flush();

            element_offsets.Clear();
            foreach (var off in offsets_arr)
            {
                element_offsets.AppendElement(off);
            }
            element_offsets.Flush();
        }

        internal IEnumerable<ObjOff> GetAllByValue(IComparable valuesample)
        {
            _ = valuesample ?? throw new ArgumentNullException(nameof(valuesample));
            EnsureValuesArrayLoaded();

            if (values_arr is null || values_arr.Length == 0)
                yield break;

            if (valueoffs_dic.TryGetValue(valuesample, out var offs))
            {
                foreach (var oo in offs!.Select(o => new ObjOff(sequence.GetByOffset(o), o)))
                {
                    yield return oo;
                }
            }

            int pos = Array.BinarySearch(values_arr, valuesample);
            if (pos >= 0)
            {
                int p = pos;
                while (p >= 0 && values_arr[p].CompareTo(valuesample) == 0)
                {
                    pos = p;
                    p--;
                }

                while (pos < values_arr.Length && values_arr[pos].CompareTo(valuesample) == 0)
                {
                    long offset = (long)element_offsets.GetByIndex(pos);
                    yield return new ObjOff(sequence.GetByOffset(offset), offset);
                    pos++;
                }
            }
        }

        private void EnsureValuesArrayLoaded()
        {
            if (values_arr != null) return;
            values_arr = values.ElementValues().Cast<IComparable>().ToArray();
        }
    }
}
