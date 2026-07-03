using Polar.DB;

namespace Polar.Universal
{
    [Obsolete]
    public class UVecIndex : IUIndex
    {
        private readonly USequence sequence;
        private Func<object, IEnumerable<IComparable>> keysFunc;
        private Func<IComparable, int> hashOfKey;

        private UniversalSequenceBase hkeys;
        private UniversalSequenceBase offsets;

        private sealed class DynPairsSet
        {
            private int[] hvalues;
            private long[] offsets;
            private USequence sequ;
            private Func<IComparable, int> hashOfKey;

            internal DynPairsSet(USequence sequ, Func<IComparable, int> hashOfKey)
            {
                this.sequ = sequ;
                this.hashOfKey = hashOfKey;
                hvalues = new int[0];
                offsets = new long[0];
            }

            internal void Clear()
            {
                hvalues = new int[0];
                offsets = new long[0];
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

                object rec = sequ.GetByOffset(offsets[ind]);
                yield return new ObjOff(rec, offsets[ind]);

                int i = ind - 1;
                while (i >= 0)
                {
                    if (hvalues[i] != hashofvaluesample) break;
                    rec = sequ.GetByOffset(offsets[i]);
                    yield return new ObjOff(rec, offsets[i]);
                    i--;
                }

                i = ind + 1;
                while (i < hvalues.Length)
                {
                    if (hvalues[i] != hashofvaluesample) break;
                    rec = sequ.GetByOffset(offsets[i]);
                    yield return new ObjOff(rec, offsets[i]);
                    i++;
                }
            }
        }

        private DynPairsSet dynindex;
        private bool keysinmemory;
        private bool ignorecase;
        private int[]? hkeys_arr = null;
        private bool disposed;

        public UVecIndex(Func<Stream> streamGen, USequence sequence,
            Func<object, IEnumerable<IComparable>> keysFunc, Func<IComparable, int> hashOfKey,
            bool ignorecase = false)
        {
            this.sequence = sequence;
            this.keysFunc = keysFunc;
            this.hashOfKey = hashOfKey;
            this.keysinmemory = false;
            this.ignorecase = ignorecase;

            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            dynindex = new DynPairsSet(sequence, hashOfKey);
        }

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            dynindex.Clear();
        }

        public void Flush()
        {
            hkeys.Flush();
            offsets.Flush();
        }

        public void Close()
        {
            Dispose();
        }

        public void Refresh()
        {
            hkeys.Refresh();
            if (keysinmemory) hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            else hkeys_arr = null;
            offsets.Refresh();
            dynindex.Clear();
        }

        public void Build()
        {
            List<int> hkeys_list = new List<int>();
            List<long> offsets_list = new List<long>();
            sequence.Scan((off, obj) =>
            {
                var keys = keysFunc(obj);
                foreach (IComparable key in keys)
                {
                    IComparable k = key;
                    if (ignorecase) k = ((string)k).ToUpper();
                    offsets_list.Add(off);
                    hkeys_list.Add(hashOfKey(k));
                }

                return true;
            });

            hkeys_arr = hkeys_list.ToArray();
            hkeys_list = null;
            long[] offsets_arr = offsets_list.ToArray();
            offsets_list = null;
            GC.Collect();

            Array.Sort(hkeys_arr, offsets_arr);

            hkeys.Clear();
            foreach (var hkey in hkeys_arr) hkeys.AppendElement(hkey);
            hkeys.Flush();
            if (!keysinmemory)
            {
                hkeys_arr = null;
                GC.Collect();
            }

            offsets.Clear();
            foreach (var off in offsets_arr) offsets.AppendElement(off);
            offsets.Flush();
            offsets_arr = null;
            GC.Collect();
        }

        public void OnAppendElement(object element, long offset)
        {
            IEnumerable<IComparable> keys = keysFunc(element);
            if (ignorecase)
            {
                keys = keys.Select(k => (IComparable)((string)k).ToUpper());
            }

            dynindex.OnAppendValues(keys.ToArray(), offset);
        }

        public IEnumerable<ObjOff> GetAllByValue(IComparable valuesample)
        {
            if (ignorecase) valuesample = ((string)valuesample).ToUpper();
            int hashofvaluesample = hashOfKey(valuesample);
            var emittedOffsets = new HashSet<long>();

            var query = dynindex.GetAllByValue(valuesample);
            foreach (var v in query)
            {
                if (emittedOffsets.Add(v.off)) yield return v;
            }

            if (hkeys_arr == null) hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            if (hkeys_arr == null || hkeys_arr.Length == 0) yield break;

            int ind = Array.BinarySearch(hkeys_arr, hashofvaluesample);
            if (ind < 0) yield break;

            long off = (long)offsets.GetByIndex(ind);
            object rec = sequence.GetByOffset(off);
            if (emittedOffsets.Add(off)) yield return new ObjOff(rec, off);

            int i = ind - 1;
            while (i >= 0)
            {
                if (hkeys_arr[i] != hashofvaluesample) break;
                off = (long)offsets.GetByIndex(i);
                rec = sequence.GetByOffset(off);
                if (emittedOffsets.Add(off)) yield return new ObjOff(rec, off);
                i--;
            }

            i = ind + 1;
            while (i < hkeys_arr.Length)
            {
                if (hkeys_arr[i] != hashofvaluesample) break;
                off = (long)offsets.GetByIndex(i);
                rec = sequence.GetByOffset(off);
                if (emittedOffsets.Add(off)) yield return new ObjOff(rec, off);
                i++;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || disposed) return;
            hkeys.Dispose();
            offsets.Dispose();
            disposed = true;
        }

    }
}
