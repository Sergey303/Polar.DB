using Polar.DB;

namespace Polar.Universal
{
    public class EKeyIndex : IUIndex
    {
        private readonly USequence sequence;
        private readonly Func<object, IEnumerable<IComparable>> keysFunc;
        private readonly Func<IComparable, int> hashOfKey;
        private readonly UniversalSequenceBase hkeys;
        private readonly UniversalSequenceBase offsets;

        private readonly Dictionary<IComparable, List<PLO>> dynamicByPrimary = new();
        private readonly Dictionary<IComparable, List<PLO>> dynamicByLocal = new();
        private readonly HashSet<IComparable> changedPrimaries = new();

        private int[] hkeys_arr = Array.Empty<int>();
        private bool disposed;

        private readonly struct PLO
        {
            public PLO(IComparable primary, IComparable local, long offset)
            {
                this.primary = primary;
                this.local = local;
                this.offset = offset;
            }

            public readonly IComparable primary;
            public readonly IComparable local;
            public readonly long offset;
        }

        public EKeyIndex(Func<Stream> streamGen, USequence sequence,
            Func<object, IEnumerable<IComparable>> keysFunc, Func<IComparable, int> hashOfKey)
        {
            this.sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            this.keysFunc = keysFunc ?? throw new ArgumentNullException(nameof(keysFunc));
            this.hashOfKey = hashOfKey ?? throw new ArgumentNullException(nameof(hashOfKey));

            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
        }

        public void OnAppendElement(object element, long offset)
        {
            var primary = sequence.GetPrimaryKey(element);
            RemoveDynamicPrimary(primary);
            changedPrimaries.Add(primary);

            List<PLO>? current = null;
            foreach (var key in DistinctKeys(keysFunc(element)))
            {
                var entry = new PLO(primary, key, offset);
                current ??= new List<PLO>();
                current.Add(entry);

                if (!dynamicByLocal.TryGetValue(key, out var localEntries))
                {
                    localEntries = new List<PLO>();
                    dynamicByLocal.Add(key, localEntries);
                }
                localEntries.Add(entry);
            }

            if (current != null)
                dynamicByPrimary[primary] = current;
        }

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = Array.Empty<int>();
            offsets.Clear();
            ClearDynamic();
        }

        public void Flush()
        {
            hkeys.Flush();
            offsets.Flush();
        }

        public void Close() => Dispose();

        public void Refresh()
        {
            hkeys.Refresh();
            offsets.Refresh();
            hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            ClearDynamic();
        }

        public void Build()
        {
            var hkeysList = new List<int>();
            var offsetsList = new List<long>();

            sequence.Scan((off, obj) =>
            {
                foreach (var localKey in DistinctKeys(keysFunc(obj)))
                {
                    offsetsList.Add(off);
                    hkeysList.Add(hashOfKey(localKey));
                }
                return true;
            });

            hkeys_arr = hkeysList.ToArray();
            var offsetsArray = offsetsList.ToArray();
            Array.Sort(hkeys_arr, offsetsArray);

            hkeys.ReplaceWithFixedInt32Array(hkeys_arr);
            offsets.ReplaceWithFixedInt64Array(offsetsArray);
            ClearDynamic();
        }

        public IEnumerable<object> GetManyByKey(IComparable localkey)
        {
            if (localkey == null) throw new ArgumentNullException(nameof(localkey));

            if (dynamicByLocal.TryGetValue(localkey, out var dynamicEntries))
            {
                foreach (var entry in dynamicEntries.ToArray())
                {
                    var value = sequence.GetByOffset(entry.offset);
                    if (!sequence.isEmpty(value))
                        yield return value;
                }
            }

            int hkey = hashOfKey(localkey);
            int pos = LowerBound(hkeys_arr, hkey);
            while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
            {
                long offset = (long)offsets.GetByIndex(pos++);
                object value = sequence.GetByOffset(offset);
                var primary = sequence.GetPrimaryKey(value);

                if (changedPrimaries.Contains(primary)) continue;
                if (sequence.isEmpty(value)) continue;
                if (!ContainsLogicalKey(value, localkey)) continue;

                yield return value;
            }
        }

        private bool ContainsLogicalKey(object element, IComparable sample)
        {
            foreach (var candidate in keysFunc(element) ?? Enumerable.Empty<IComparable>())
            {
                if (EqualityComparer<IComparable>.Default.Equals(candidate, sample))
                    return true;
            }
            return false;
        }

        private static IEnumerable<IComparable> DistinctKeys(IEnumerable<IComparable>? keys)
        {
            if (keys == null) yield break;

            IComparable? first = null;
            var hasFirst = false;
            HashSet<IComparable>? seen = null;

            foreach (var key in keys)
            {
                if (key == null)
                    throw new InvalidDataException("External index key cannot be null.");

                if (!hasFirst)
                {
                    first = key;
                    hasFirst = true;
                    yield return key;
                    continue;
                }

                seen ??= new HashSet<IComparable> { first! };
                if (seen.Add(key))
                    yield return key;
            }
        }

        private void RemoveDynamicPrimary(IComparable primary)
        {
            if (!dynamicByPrimary.TryGetValue(primary, out var oldEntries)) return;

            foreach (var old in oldEntries)
            {
                if (!dynamicByLocal.TryGetValue(old.local, out var localEntries)) continue;
                for (var i = localEntries.Count - 1; i >= 0; i--)
                {
                    if (EqualityComparer<IComparable>.Default.Equals(localEntries[i].primary, primary))
                        localEntries.RemoveAt(i);
                }
                if (localEntries.Count == 0)
                    dynamicByLocal.Remove(old.local);
            }

            dynamicByPrimary.Remove(primary);
        }

        private void ClearDynamic()
        {
            dynamicByPrimary.Clear();
            dynamicByLocal.Clear();
            changedPrimaries.Clear();
        }

        private static int LowerBound(int[] values, int value)
        {
            var left = 0;
            var right = values.Length;
            while (left < right)
            {
                var middle = left + (right - left) / 2;
                if (values[middle] < value) left = middle + 1;
                else right = middle;
            }
            return left;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || disposed) return;
            hkeys.Dispose();
            offsets.Dispose();
            disposed = true;
        }
    }
}
