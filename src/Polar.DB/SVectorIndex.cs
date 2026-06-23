using Polar.DB;

namespace Polar.Universal
{
    public class SVectorIndex : IUIndex
    {
        private readonly bool ignorecase = true;
        private readonly USequence sequence;
        private Func<object, IEnumerable<string>> valuesFunc;

        private UniversalSequenceBase values;
        private UniversalSequenceBase element_offsets;

        public static Comparer<string> comp_string = Comparer<string>.Create(new Comparison<string>((string v1, string v2) =>
        {
            string a = v1;
            string b = v2;
            return string.Compare(a, b, StringComparison.Ordinal);
        }));

        public static Comparer<string> comp_string_like = Comparer<string>.Create(new Comparison<string>((string v1, string v2) =>
        {
            string a = v1;
            string b = v2;
            if (string.IsNullOrEmpty(b)) return 0;
            int len = b.Length;
            return string.Compare(a, 0, b, 0, len, StringComparison.Ordinal);
        }));

        private sealed class DynPairsSet
        {
            private string[] svalues;
            private long[] offsets;
            private USequence sequ;

            internal DynPairsSet(USequence sequ)
            {
                this.sequ = sequ;
                svalues = new string[0];
                offsets = new long[0];
            }

            internal void Clear()
            {
                svalues = new string[0];
                offsets = new long[0];
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

                object rec = sequ.GetByOffset(offsets[ind]);
                yield return new ObjOff(rec, offsets[ind]);

                int i = ind - 1;
                while (i >= 0)
                {
                    if (comp_s.Compare(svalues[i], valuesample) != 0) break;
                    rec = sequ.GetByOffset(offsets[i]);
                    yield return new ObjOff(rec, offsets[i]);
                    i--;
                }

                i = ind + 1;
                while (i < svalues.Length)
                {
                    if (comp_s.Compare(svalues[i], valuesample) != 0) break;
                    rec = sequ.GetByOffset(offsets[i]);
                    yield return new ObjOff(rec, offsets[i]);
                    i++;
                }
            }

            internal IEnumerable<ObjOff> GetAllByValue(string valuesample) => GetAllByComp(valuesample, comp_string);
            internal IEnumerable<ObjOff> GetAllByLike(string valuesample) => GetAllByComp(valuesample, comp_string_like);
        }

        private DynPairsSet dynindex;
        private string[] values_arr = null;
        private bool disposed;

        public SVectorIndex(Func<Stream> streamGen, USequence sequence, Func<object, IEnumerable<string>> valuesFunc, bool ignorecase = true)
        {
            this.ignorecase = ignorecase;
            this.sequence = sequence;
            this.valuesFunc = valuesFunc;

            values = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), streamGen());
            element_offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            dynindex = new DynPairsSet(sequence);
        }

        public void Clear()
        {
            values.Clear();
            element_offsets.Clear();
            values_arr = new string[0];
            dynindex.Clear();
        }

        public void Flush()
        {
            values.Flush();
            element_offsets.Flush();
        }

        public void Close()
        {
            Dispose();
        }

        public void Refresh()
        {
            values.Refresh();
            values_arr = values.ElementValues().Cast<string>().ToArray();
            element_offsets.Refresh();
            dynindex.Clear();
        }

        public void Build()
        {
            List<string> values_list = new List<string>();
            List<long> offsets_list = new List<long>();
            sequence.Scan((off, obj) =>
            {
                var vals = valuesFunc(obj);
                foreach (var v in vals)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    offsets_list.Add(off);
                    values_list.Add(v);
                }

                return true;
            });

            values_arr = values_list
                .Select(s => ignorecase ? s.ToUpper() : s)
                .ToArray();
            values_list = null;
            long[] offsets_arr = offsets_list.ToArray();
            offsets_list = null;
            GC.Collect();

            Array.Sort(values_arr, offsets_arr, StringComparer.Ordinal);

            values.Clear();
            foreach (var v in values_arr) values.AppendElement(v);
            values.Flush();

            element_offsets.Clear();
            foreach (var off in offsets_arr) element_offsets.AppendElement(off);
            element_offsets.Flush();
            offsets_arr = null;
            GC.Collect();
        }

        public void OnAppendElement(object element, long offset)
        {
            var values = valuesFunc(element)
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => ignorecase ? v.ToUpper() : v);
            dynindex.OnAppendValues(values.ToArray(), offset);
        }

        private IEnumerable<ObjOff> GetAllByComp(string valuesample, Comparer<string> comp_s)
        {
            if (values_arr == null) values_arr = values.ElementValues().Cast<string>().ToArray();
            if (values_arr.Length == 0) yield break;

            int ind = Array.BinarySearch(values_arr, valuesample, comp_s);
            if (ind < 0) yield break;

            long off = (long)element_offsets.GetByIndex(ind);
            object rec = sequence.GetByOffset(off);
            yield return new ObjOff(rec, off);

            int i = ind - 1;
            while (i >= 0)
            {
                if (comp_s.Compare(values_arr[i], valuesample) != 0) break;
                off = (long)element_offsets.GetByIndex(i);
                rec = sequence.GetByOffset(off);
                yield return new ObjOff(rec, off);
                i--;
            }

            i = ind + 1;
            while (i < values_arr.Length)
            {
                if (comp_s.Compare(values_arr[i], valuesample) != 0) break;
                off = (long)element_offsets.GetByIndex(i);
                rec = sequence.GetByOffset(off);
                yield return new ObjOff(rec, off);
                i++;
            }
        }

        internal IEnumerable<ObjOff> GetAllByValue(string valuesample)
        {
            string svalue = ignorecase ? valuesample.ToUpper() : valuesample;

            var query = dynindex.GetAllByValue(svalue);
            foreach (var v in query) yield return v;

            var qu = GetAllByComp(svalue, comp_string);
            foreach (var v in qu) yield return v;
        }

        internal IEnumerable<ObjOff> GetAllByLike(string svalue)
        {
            if (ignorecase) svalue = svalue.ToUpper();

            var query = dynindex.GetAllByLike(svalue);
            foreach (var v in query) yield return v;

            var qu = GetAllByComp(svalue, comp_string_like);
            foreach (var v in qu) yield return v;
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || disposed) return;
            values.Dispose();
            element_offsets.Dispose();
            disposed = true;
        }

    }
}
