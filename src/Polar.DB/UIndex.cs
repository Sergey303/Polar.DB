using Polar.DB;

namespace Polar.Universal
{
    internal struct HKeyObjOff
    {
        public int hkey;
        public object obj;
        public long off;
    }

    public class UIndex : IUIndex
    {
        private readonly USequence sequence;

        // Параметры конструктора
        private Func<object, bool> applicable;
        private Func<object, int> hashFunc;
        private Comparer<object> comp;

        // Статическая часть индекса
        private UniversalSequenceBase hkeys;
        private UniversalSequenceBase offsets;

        // Динамическая часть индекса: множество троек и компаратор
        private HKeyObjOff[] dynset;
        private Comparer<HKeyObjOff> complex_comp;

        public UIndex(Func<Stream> streamGen, USequence sequence,
            Func<object, bool> applicable, Func<object, int> hashFunc, Comparer<object> comp)
        {
            this.sequence = sequence;
            this.applicable = applicable;
            this.hashFunc = hashFunc;
            this.comp = comp;

            if (hashFunc != null) hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            complex_comp = Comparer<HKeyObjOff>.Create(new Comparison<HKeyObjOff>((HKeyObjOff h1, HKeyObjOff h2) =>
            {
                int cmp;
                if (hashFunc != null)
                {
                    cmp = h1.hkey.CompareTo(h2.hkey);
                    if (cmp != 0) return cmp;
                }

                return comp.Compare(h1.obj, h2.obj);
            }));
            dynset = new HKeyObjOff[0];
        }

        private int[] hkeys_arr = null;
        private Comparer<long> comp_spec_long;
        private bool disposed;

        public void Clear()
        {
            if (hashFunc != null) hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            dynset = new HKeyObjOff[0];
        }

        public void Flush()
        {
            if (hashFunc != null) hkeys.Flush();
            offsets.Flush();
        }

        public void Close()
        {
            Dispose();
        }

        public void Refresh()
        {
            if (hashFunc != null)
            {
                hkeys.Refresh();
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            }

            offsets.Refresh();
            dynset = new HKeyObjOff[0];
        }

        public void Build()
        {
            if (hashFunc == null) BuildOffsets();
            else BuildHkeyOffsets();
        }

        private void BuildOffsets()
        {
            comp_spec_long = Comparer<long>.Create(new Comparison<long>((off1, off2) =>
            {
                object v1 = sequence.GetByOffset(off1);
                object v2 = sequence.GetByOffset(off2);
                return comp.Compare(v1, v2);
            }));
            // сканируем опорную последовательность, формируем массивы
            List<long> offsets_list = new ();
            sequence.Scan((off, obj) =>
            {
                if (applicable(obj)) offsets_list.Add(off);
                return true;
            });

            long[] offsets_arr = offsets_list.ToArray();
            offsets_list = null;
            GC.Collect();

            Array.Sort(offsets_arr, comp_spec_long);

            offsets.Clear();
            foreach (var off in offsets_arr) offsets.AppendElement(off);
            offsets.Flush();
            offsets_arr = null;
            GC.Collect();
        }

        private void BuildHkeyOffsets()
        {
            // сканируем опорную последовательность, формируем массивы
            List<int> hkeys_list = new ();
            List<long> offsets_list = new ();
            sequence.Scan((off, obj) =>
            {
                if (applicable(obj))
                {
                    offsets_list.Add(off);
                    hkeys_list.Add(hashFunc(obj));
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

            offsets.Clear();
            foreach (var off in offsets_arr) offsets.AppendElement(off);
            offsets.Flush();
            offsets_arr = null;
            GC.Collect();
        }

        internal IEnumerable<ObjOff> GetAllBySample(object sample)
        {
            if (dynset.Length > 0)
            {
                HKeyObjOff complex_sample = new () { obj = sample };
                if (hashFunc != null) complex_sample.hkey = hashFunc(sample);

                var query = dynset.Where(hoo => complex_comp.Compare(hoo, complex_sample) == 0)
                    .Select(hoo => new ObjOff(hoo.obj, hoo.off));
                foreach (var oo in query)
                {
                    yield return oo;
                }
            }

            if (hashFunc != null)
            {
                int hsample = hashFunc(sample);
                long firstByHash = GetFirstNom(hsample);
                if (firstByHash == -1) yield break;

                for (long ii = firstByHash; ii < hkeys.Count(); ii++)
                {
                    int hkey = (int)hkeys.GetByIndex(ii);
                    if (hkey != hsample) break;

                    long off = (long)offsets.GetByIndex(ii);
                    object value = sequence.GetByOffset(off);
                    if (comp.Compare(value, sample) == 0) yield return new ObjOff(value, off);
                }

                yield break;
            }

            long first = GetFirstNomOffsets(sample, comp);
            if (first == -1) yield break;
            for (long ii = first; ii < offsets.Count(); ii++)
            {
                long off = (long)offsets.GetByIndex(ii);
                object value = sequence.GetByOffset(off);
                if (comp.Compare(value, sample) == 0) yield return new ObjOff(value, off);
                else break;
            }
        }

        internal IEnumerable<ObjOff> GetAllByLike(object sample, Comparer<object> comp_like)
        {
            if (dynset.Length > 0)
            {
                var query = dynset.Select(hoo => new ObjOff(hoo.obj, hoo.off));
                foreach (var oo in query)
                {
                    if (comp_like.Compare(oo.obj, sample) == 0) yield return oo;
                }
            }

            long first = GetFirstNomOffsets(sample, comp_like);
            if (first == -1) yield break;
            for (long ii = first; ii < offsets.Count(); ii++)
            {
                long off = (long)offsets.GetByIndex(ii);
                object value = sequence.GetByOffset(off);
                if (comp_like.Compare(value, sample) == 0) yield return new ObjOff(value, off);
                else break;
            }
        }

        public void OnAppendElement(object element, long offset)
        {
            if (!applicable(element)) return;

            HKeyObjOff item = new HKeyObjOff() { obj = element, off = offset };
            if (hashFunc != null) item.hkey = hashFunc(element);

            HKeyObjOff[] updated = new HKeyObjOff[dynset.Length + 1];
            Array.Copy(dynset, updated, dynset.Length);
            updated[updated.Length - 1] = item;
            Array.Sort(updated, complex_comp);
            dynset = updated;
        }

        private long GetFirstNomOffsets(object sample, Comparer<object> comparer)
        {
            long count = offsets.Count();
            long left = 0;
            long right = count;

            while (left < right)
            {
                long middle = left + (right - left) / 2;
                object middle_value = sequence.GetByOffset((long)offsets.GetByIndex(middle));
                int cmp = comparer.Compare(middle_value, sample);
                if (cmp < 0) left = middle + 1;
                else right = middle;
            }

            if (left >= count) return -1;
            object value = sequence.GetByOffset((long)offsets.GetByIndex(left));
            return comparer.Compare(value, sample) == 0 ? left : -1;
        }

        /// <summary>
        /// Определение номера первого индекса последовательности hkeys, с которого значения РАВНЫ hkey (хешу от ключа).
        /// Если нет таких, то -1L.
        /// </summary>
        private long GetFirstNom(int hkey)
        {
            long count = hkeys.Count();
            long left = 0;
            long right = count;

            while (left < right)
            {
                long middle = left + (right - left) / 2;
                int middleValue = (int)hkeys.GetByIndex(middle);
                if (middleValue < hkey) left = middle + 1;
                else right = middle;
            }

            if (left >= count) return -1;
            return (int)hkeys.GetByIndex(left) == hkey ? left : -1;
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || disposed) return;
            if (hashFunc != null) hkeys.Dispose();
            offsets.Dispose();
            disposed = true;
        }

    }
}
