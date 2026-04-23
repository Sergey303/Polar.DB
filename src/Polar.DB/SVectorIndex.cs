namespace Polar.DB
{
    /// <summary>
    /// String-specialized secondary index for multi-valued textual keys.
    /// </summary>
    /// <remarks>
    /// The index keeps a sorted static persisted part plus a sorted dynamic in-memory part for appended elements.
    /// Hashes are not used here; lookups are based on ordinal string comparison and optional prefix matching.
    /// </remarks>
    public class SVectorIndex : IUIndex
    {
        private readonly bool ignorecase = true;
        private readonly USequence sequence;
        private readonly Func<object, IEnumerable<string>> valuesFunc;
        private readonly UniversalSequenceBase values;
        private readonly UniversalSequenceBase element_offsets;

        /// <summary>
        /// Ordinal string comparer used for exact-match lookup ordering.
        /// </summary>
        public static Comparer<string> comp_string = Comparer<string>.Create((v1, v2) =>
            string.Compare(v1, v2, StringComparison.Ordinal));

        /// <summary>
        /// Ordinal prefix comparer where the right-hand side is treated as a prefix sample.
        /// </summary>
        public static Comparer<string> comp_string_like = Comparer<string>.Create((v1, v2) =>
        {
            if (string.IsNullOrEmpty(v2)) return 0;
            return string.Compare(v1, 0, v2, 0, v2.Length, StringComparison.Ordinal);
        });

        private sealed class DynPairsSet
        {
            private string[] svalues;
            private long[] offsets;
            private readonly USequence sequ;

            internal DynPairsSet(USequence sequ)
            {
                this.sequ = sequ;
                svalues = Array.Empty<string>();
                offsets = Array.Empty<long>();
            }

            internal void Clear()
            {
                svalues = Array.Empty<string>();
                offsets = Array.Empty<long>();
            }

            internal void OnAppendValues(string[] adds, long offset)
            {
                int len = svalues.Length;
                int nplus = adds.Length;
                if (nplus == 0) return;

                string[] vals = new string[len + nplus];
                long[] offs = new long[len + nplus];
                for (int i = 0; i < len; i++)
                {
                    vals[i] = svalues[i];
                    offs[i] = offsets[i];
                }
                for (int i = 0; i < nplus; i++)
                {
                    vals[len + i] = adds[i];
                    offs[len + i] = offset;
                }

                Array.Sort(vals, offs, comp_string);
                svalues = vals;
                offsets = offs;
            }

            private IEnumerable<ObjOff> GetAllByComp(string valuesample, Comparer<string> comp_s)
            {
                int ind = Array.BinarySearch(svalues, valuesample, comp_s);
                if (ind < 0) yield break;

                yield return new ObjOff(sequ.GetByOffset(offsets[ind]), offsets[ind]);

                int i = ind - 1;
                while (i >= 0)
                {
                    if (comp_s.Compare(svalues[i], valuesample) != 0) break;
                    yield return new ObjOff(sequ.GetByOffset(offsets[i]), offsets[i]);
                    i--;
                }

                i = ind + 1;
                while (i < svalues.Length)
                {
                    if (comp_s.Compare(svalues[i], valuesample) != 0) break;
                    yield return new ObjOff(sequ.GetByOffset(offsets[i]), offsets[i]);
                    i++;
                }
            }

            internal IEnumerable<ObjOff> GetAllByValue(string valuesample) => GetAllByComp(valuesample, comp_string);
            internal IEnumerable<ObjOff> GetAllByLike(string valuesample) => GetAllByComp(valuesample, comp_string_like);
        }

        private readonly DynPairsSet dynindex;

        /// <summary>
        /// Creates a string vector index.
        /// </summary>
        /// <param name="streamGen">Factory for streams used by persisted index parts.</param>
        /// <param name="sequence">Owner sequence whose elements are indexed.</param>
        /// <param name="valuesFunc">Extractor returning one or many strings for each sequence element.</param>
        /// <param name="ignorecase">When <see langword="true"/>, index normalizes strings to uppercase before storing and searching.</param>
        public SVectorIndex(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, IEnumerable<string>> valuesFunc,
            bool ignorecase = true)
        {
            this.ignorecase = ignorecase;
            this.sequence = sequence;
            this.valuesFunc = valuesFunc;

            values = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), streamGen());
            element_offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            dynindex = new DynPairsSet(sequence);
        }

        private string[]? values_arr;

        /// <summary>
        /// Clears static and dynamic index state.
        /// </summary>
        public void Clear()
        {
            values.Clear();
            element_offsets.Clear();
            values_arr = Array.Empty<string>();
            dynindex.Clear();
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
            values_arr = values.ElementValues().Cast<string>().ToArray();
            element_offsets.Refresh();
        }

        /// <summary>
        /// Rebuilds static index state from the owner sequence logical view.
        /// </summary>
        public void Build()
        {
            BuildFromSnapshot(sequence.CreateLogicalBuildSnapshot());
        }

        internal void BuildFromSnapshot(IReadOnlyList<USequence.LogicalBuildEntry> snapshot)
        {
            List<string> values_list = new List<string>();
            List<long> offsets_list = new List<long>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                var vals = valuesFunc(entry.Element);
                foreach (var v in vals)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    offsets_list.Add(entry.Offset);
                    values_list.Add(v);
                }
            }

            values_arr = values_list
                .Select(s => ignorecase ? s.ToUpper() : s)
                .ToArray();
            long[] offsets_arr = offsets_list.ToArray();

            Array.Sort(values_arr, offsets_arr, StringComparer.Ordinal);

            values.Clear();
            values.AppendElements(values_arr.Select(static x => (object)x));
            values.Flush();

            element_offsets.Clear();
            element_offsets.AppendElements(offsets_arr.Select(static x => (object)x));
            element_offsets.Flush();
        }

        /// <summary>
        /// Appends extracted values of one newly appended element to the dynamic in-memory state.
        /// </summary>
        /// <param name="element">Appended sequence element.</param>
        /// <param name="offset">Physical stream offset of the appended element.</param>
        public void OnAppendElement(object element, long offset)
        {
            var vals = valuesFunc(element).Select(v => ignorecase ? v.ToUpper() : v).ToArray();
            dynindex.OnAppendValues(vals, offset);
        }

        private IEnumerable<ObjOff> GetAllByComp(string valuesample, Comparer<string> comp_s)
        {
            EnsureValuesArrayLoaded();
            if (values_arr is null || values_arr.Length == 0)
                yield break;

            int ind = Array.BinarySearch(values_arr, valuesample, comp_s);
            if (ind < 0) yield break;

            long off = (long)element_offsets.GetByIndex(ind);
            yield return new ObjOff(sequence.GetByOffset(off), off);

            int i = ind - 1;
            while (i >= 0)
            {
                if (comp_s.Compare(values_arr[i], valuesample) != 0) break;
                off = (long)element_offsets.GetByIndex(i);
                yield return new ObjOff(sequence.GetByOffset(off), off);
                i--;
            }

            i = ind + 1;
            while (i < values_arr.Length)
            {
                if (comp_s.Compare(values_arr[i], valuesample) != 0) break;
                off = (long)element_offsets.GetByIndex(i);
                yield return new ObjOff(sequence.GetByOffset(off), off);
                i++;
            }
        }

        internal IEnumerable<ObjOff> GetAllByValue(string valueSample)
        {
            string sValueNormalized = ignorecase ? valueSample.ToUpper() : valueSample;

            foreach (var v in dynindex.GetAllByValue(sValueNormalized))
                yield return v;

            foreach (var v in GetAllByComp(sValueNormalized, comp_string))
                yield return v;
        }

        internal IEnumerable<ObjOff> GetAllByLike(string svalue)
        {
            if (ignorecase) svalue = svalue.ToUpper();

            foreach (var v in dynindex.GetAllByLike(svalue))
                yield return v;

            foreach (var v in GetAllByComp(svalue, comp_string_like))
                yield return v;
        }

        private void EnsureValuesArrayLoaded()
        {
            if (values_arr != null) return;
            values_arr = values.ElementValues().Cast<string>().ToArray();
        }
    }
}
