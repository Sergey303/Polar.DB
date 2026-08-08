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
        private readonly Func<object, bool> applicable;
        private readonly Func<object, int>? hashFunc;
        private readonly Comparer<object> comp;

        private readonly UniversalSequenceBase? hkeys;
        private readonly UniversalSequenceBase offsets;

        // Appends stay O(1). The historical sorted enumeration order is restored lazily
        // before the next dynamic query instead of re-sorting after every append.
        private readonly List<HKeyObjOff> dynset = new();
        private readonly Comparer<HKeyObjOff> complexComp;
        private bool dynsetSorted = true;

        private int[]? hkeysArr;
        private Comparer<long>? compSpecLong;
        private bool disposed;

        public UIndex(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, bool> applicable,
            Func<object, int>? hashFunc,
            Comparer<object> comp)
        {
            _ = streamGen ?? throw new ArgumentNullException(nameof(streamGen));
            this.sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            this.applicable = applicable ?? throw new ArgumentNullException(nameof(applicable));
            this.hashFunc = hashFunc;
            this.comp = comp ?? throw new ArgumentNullException(nameof(comp));

            if (hashFunc != null)
                hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            complexComp = Comparer<HKeyObjOff>.Create((left, right) =>
            {
                if (this.hashFunc != null)
                {
                    int hashComparison = left.hkey.CompareTo(right.hkey);
                    if (hashComparison != 0) return hashComparison;
                }

                return this.comp.Compare(left.obj, right.obj);
            });
        }

        public void Clear()
        {
            hkeys?.Clear();
            hkeysArr = null;
            offsets.Clear();
            ClearDynamic();
        }

        public void Flush()
        {
            hkeys?.Flush();
            offsets.Flush();
        }

        public void Close() => Dispose();

        public void Refresh()
        {
            if (hashFunc != null)
            {
                hkeys!.Refresh();
                hkeysArr = hkeys.ElementValues().Cast<int>().ToArray();
            }

            offsets.Refresh();
            ClearDynamic();
        }

        public void Build()
        {
            if (hashFunc == null) BuildOffsets();
            else BuildHkeyOffsets();

            // Build scans the complete current sequence, so all dynamic rows are now
            // represented by the static snapshot and must not be emitted twice.
            ClearDynamic();
        }

        private void BuildOffsets()
        {
            compSpecLong = Comparer<long>.Create((off1, off2) =>
            {
                object v1 = sequence.GetByOffset(off1);
                object v2 = sequence.GetByOffset(off2);
                return comp.Compare(v1, v2);
            });

            var offsetsList = new List<long>();
            sequence.Scan((off, obj) =>
            {
                if (applicable(obj)) offsetsList.Add(off);
                return true;
            });

            long[] offsetsArray = offsetsList.ToArray();
            Array.Sort(offsetsArray, compSpecLong);
            offsets.ReplaceWithFixedInt64Array(offsetsArray);
        }

        private void BuildHkeyOffsets()
        {
            var hkeysList = new List<int>();
            var offsetsList = new List<long>();
            sequence.Scan((off, obj) =>
            {
                if (applicable(obj))
                {
                    offsetsList.Add(off);
                    hkeysList.Add(hashFunc!(obj));
                }

                return true;
            });

            hkeysArr = hkeysList.ToArray();
            long[] offsetsArray = offsetsList.ToArray();
            Array.Sort(hkeysArr, offsetsArray);

            hkeys!.ReplaceWithFixedInt32Array(hkeysArr);
            offsets.ReplaceWithFixedInt64Array(offsetsArray);
        }

        internal IEnumerable<ObjOff> GetAllBySample(object sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            if (dynset.Count > 0)
            {
                EnsureDynamicSorted();
                var dynamicSample = new HKeyObjOff { obj = sample };
                if (hashFunc != null) dynamicSample.hkey = hashFunc(sample);

                int start = LowerBoundDynamic(dynamicSample);
                for (int i = start; i < dynset.Count && complexComp.Compare(dynset[i], dynamicSample) == 0; i++)
                    yield return new ObjOff(dynset[i].obj, dynset[i].off);
            }

            if (hashFunc != null)
            {
                int hashSample = hashFunc(sample);
                long firstByHash = GetFirstNom(hashSample);
                if (firstByHash == -1) yield break;

                long count = hkeysArr?.LongLength ?? hkeys!.Count();
                for (long ii = firstByHash; ii < count; ii++)
                {
                    int hashKey = hkeysArr != null
                        ? hkeysArr[(int)ii]
                        : (int)hkeys!.GetByIndex(ii);
                    if (hashKey != hashSample) break;

                    long off = (long)offsets.GetByIndex(ii);
                    object value = sequence.GetByOffset(off);
                    if (comp.Compare(value, sample) == 0)
                        yield return new ObjOff(value, off);
                }

                yield break;
            }

            long first = GetFirstNomOffsets(sample, comp);
            if (first == -1) yield break;
            for (long ii = first; ii < offsets.Count(); ii++)
            {
                long off = (long)offsets.GetByIndex(ii);
                object value = sequence.GetByOffset(off);
                if (comp.Compare(value, sample) == 0)
                    yield return new ObjOff(value, off);
                else
                    break;
            }
        }

        internal IEnumerable<ObjOff> GetAllByLike(object sample, Comparer<object> compLike)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));
            if (compLike == null) throw new ArgumentNullException(nameof(compLike));

            if (dynset.Count > 0)
            {
                EnsureDynamicSorted();
                foreach (var item in dynset)
                {
                    if (compLike.Compare(item.obj, sample) == 0)
                        yield return new ObjOff(item.obj, item.off);
                }
            }

            long first = GetFirstNomOffsets(sample, compLike);
            if (first == -1) yield break;
            for (long ii = first; ii < offsets.Count(); ii++)
            {
                long off = (long)offsets.GetByIndex(ii);
                object value = sequence.GetByOffset(off);
                if (compLike.Compare(value, sample) == 0)
                    yield return new ObjOff(value, off);
                else
                    break;
            }
        }

        public void OnAppendElement(object element, long offset)
        {
            if (!applicable(element)) return;

            var item = new HKeyObjOff { obj = element, off = offset };
            if (hashFunc != null) item.hkey = hashFunc(element);

            dynset.Add(item);
            dynsetSorted = dynset.Count <= 1;
        }

        private void EnsureDynamicSorted()
        {
            if (dynsetSorted) return;
            dynset.Sort(complexComp);
            dynsetSorted = true;
        }

        private int LowerBoundDynamic(HKeyObjOff sample)
        {
            int left = 0;
            int right = dynset.Count;
            while (left < right)
            {
                int middle = left + (right - left) / 2;
                if (complexComp.Compare(dynset[middle], sample) < 0)
                    left = middle + 1;
                else
                    right = middle;
            }
            return left;
        }

        private void ClearDynamic()
        {
            dynset.Clear();
            dynsetSorted = true;
        }

        private long GetFirstNomOffsets(object sample, Comparer<object> comparer)
        {
            long count = offsets.Count();
            long left = 0;
            long right = count;

            while (left < right)
            {
                long middle = left + (right - left) / 2;
                object middleValue = sequence.GetByOffset((long)offsets.GetByIndex(middle));
                int cmp = comparer.Compare(middleValue, sample);
                if (cmp < 0) left = middle + 1;
                else right = middle;
            }

            if (left >= count) return -1;
            object value = sequence.GetByOffset((long)offsets.GetByIndex(left));
            return comparer.Compare(value, sample) == 0 ? left : -1;
        }

        private long GetFirstNom(int hkey)
        {
            if (hkeysArr != null)
            {
                int left = 0;
                int right = hkeysArr.Length;
                while (left < right)
                {
                    int middle = left + (right - left) / 2;
                    if (hkeysArr[middle] < hkey) left = middle + 1;
                    else right = middle;
                }

                if (left >= hkeysArr.Length || hkeysArr[left] != hkey) return -1L;
                return left;
            }

            long count = hkeys!.Count();
            long diskLeft = 0;
            long diskRight = count;
            while (diskLeft < diskRight)
            {
                long middle = diskLeft + (diskRight - diskLeft) / 2;
                int middleValue = (int)hkeys.GetByIndex(middle);
                if (middleValue < hkey) diskLeft = middle + 1;
                else diskRight = middle;
            }

            if (diskLeft >= count) return -1L;
            return (int)hkeys.GetByIndex(diskLeft) == hkey ? diskLeft : -1L;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || disposed) return;
            hkeys?.Dispose();
            offsets.Dispose();
            disposed = true;
        }
    }
}
