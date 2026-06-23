using Polar.DB;

namespace Polar.Universal
{
    public class UKeyIndex : IDisposable
    {
        private readonly USequence sequence;
        private Func<object, IComparable> keyFunc;
        private Func<IComparable, int> hashOfKey;
        private UniversalSequenceBase hkeys;
        private UniversalSequenceBase offsets;
        private Dictionary<IComparable, long> keyoff_dic;
        internal bool ElementChanged(IComparable key) { return keyoff_dic.ContainsKey(key); }
        private bool keysinmemory;
        private int[] hkeys_arr = null;
        private bool disposed;

        public UIndexBuildProfile LastBuildProfile { get; private set; } = UIndexBuildProfile.Empty;

        public UKeyIndex(Func<Stream> streamGen, USequence sequence,
            Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool keysinmemory = true)
        {
            this.sequence = sequence;
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            this.keysinmemory = keysinmemory;
            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            keyoff_dic = new Dictionary<IComparable, long>();
        }

        public void OnAppendElement(object element, long offset)
        {
            var key = keyFunc(element);
            if (keyoff_dic.ContainsKey(key)) keyoff_dic.Remove(key);
            keyoff_dic.Add(key, offset);
        }

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            keyoff_dic.Clear();
            LastBuildProfile = UIndexBuildProfile.Empty;
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
            offsets.Refresh();
            hkeys_arr = keysinmemory ? hkeys.ElementValues().Cast<int>().ToArray() : null;
        }

        public void Build()
        {
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
            var scanMs = 0.0;
            var toArrayMs = 0.0;
            var sortMs = 0.0;
            var writeHashKeysMs = 0.0;
            var writeOffsetsMs = 0.0;
            var gcMs = 0.0;

            List<int> hkeys_list = new List<int>();
            List<long> offsets_list = new List<long>();
            scanMs = Measure(() =>
            {
                sequence.Scan((off, obj) =>
                {
                    offsets_list.Add(off);
                    hkeys_list.Add(hashOfKey(keyFunc(obj)));
                    return true;
                });
            });

            long[] offsets_arr = Array.Empty<long>();
            toArrayMs = Measure(() =>
            {
                hkeys_arr = hkeys_list.ToArray();
                hkeys_list = null;
                offsets_arr = offsets_list.ToArray();
                offsets_list = null;
            });

            gcMs += Measure(() => GC.Collect());
            sortMs = Measure(() => Array.Sort(hkeys_arr, offsets_arr));

            writeHashKeysMs = Measure(() =>
            {
                hkeys.ReplaceWithFixedInt32Array(hkeys_arr);
                if (!keysinmemory) hkeys_arr = null;
            });

            if (!keysinmemory) gcMs += Measure(() => GC.Collect());

            writeOffsetsMs = Measure(() =>
            {
                offsets.ReplaceWithFixedInt64Array(offsets_arr);
            });

            offsets_arr = null;
            gcMs += Measure(() => GC.Collect());
            totalWatch.Stop();

            LastBuildProfile = new UIndexBuildProfile(
                scanMs, toArrayMs, sortMs, writeHashKeysMs, writeOffsetsMs,
                gcMs, totalWatch.Elapsed.TotalMilliseconds);
        }

        public object GetByKey(IComparable keysample)
        {
            if (keyoff_dic.TryGetValue(keysample, out long off))
            {
                return sequence.GetByOffset(off);
            }

            int hkey = hashOfKey(keysample);

            if (hkeys_arr != null)
            {
                int pos = Array.BinarySearch<int>(hkeys_arr, hkey);
                if (pos < 0) return null;
                int p = pos;
                while (p >= 0 && hkeys_arr[p] == hkey)
                {
                    pos = p;
                    p--;
                }

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);
                    object val = sequence.GetByOffset(offset);
                    if (val == null) return null;
                    var k = keyFunc(val);
                    if (k.CompareTo(keysample) == 0) return val;
                    pos++;
                }

                return null;
            }

            long first = GetFirstNom(hkey);
            if (first == -1) return null;
            for (long nom = first; nom < hkeys.Count(); nom++)
            {
                long offset = (long)offsets.GetByIndex(nom);
                object val = sequence.GetByOffset(offset);
                if (val == null) break;
                var k = keyFunc(val);
                if (hashOfKey(k) != hkey) break;
                if (k.CompareTo(keysample) == 0) return val;
            }

            return null;
        }

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

        public bool IsOriginal(IComparable key, long offset)
        {
            if (keyoff_dic.TryGetValue(key, out long off)) return off == offset;
            return true;
        }

        public object GetExactlyOneByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));
            var offset = GetExactlyOneOffsetByKey(keysample);
            var value = sequence.GetByOffset(offset);
            if (value == null)
                throw new InvalidOperationException(
                    $"Expected exactly one Polar.DB element for key '{keysample}', but payload at offset {offset} is null.");

            var key = keyFunc(value);
            if (key.CompareTo(keysample) != 0 || !sequence.IsOriginalAndNotEmpty(value, offset))
                throw new InvalidOperationException(
                    $"Expected exactly one Polar.DB element for key '{keysample}', but payload at offset {offset} did not validate.");

            return value;
        }

        public IEnumerable<object> GetAllByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));
            foreach (var offset in GetOffsetsByKey(keysample))
            {
                var value = sequence.GetByOffset(offset);
                if (value == null) continue;
                var key = keyFunc(value);
                if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(value, offset))
                    yield return value;
            }
        }

        public IReadOnlyList<long> GetOffsetsByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));
            if (keyoff_dic.TryGetValue(keysample, out long dynamicOffset)) return new[] { dynamicOffset };
            return GetOffsetsByHashCompatiblePath(keysample);
        }

        public int CountByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));
            return GetOffsetsByKey(keysample).Count;
        }

        public bool TryGetExactlyOneOffsetByKey(IComparable keysample, out long offset)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));
            var offsetsByKey = GetOffsetsByKey(keysample);
            if (offsetsByKey.Count == 1)
            {
                offset = offsetsByKey[0];
                return true;
            }

            offset = default;
            return false;
        }

        public long GetExactlyOneOffsetByKey(IComparable keysample)
        {
            if (TryGetExactlyOneOffsetByKey(keysample, out var offset)) return offset;
            var count = CountByKey(keysample);
            throw new InvalidOperationException(
                $"Expected exactly one Polar.DB element offset for key '{keysample}', but found {count}.");
        }

        private IReadOnlyList<long> GetOffsetsByHashCompatiblePath(IComparable keysample)
        {
            var result = new List<long>();
            int hkey = hashOfKey(keysample);

            if (hkeys_arr != null)
            {
                int pos = Array.BinarySearch(hkeys_arr, hkey);
                if (pos < 0) return result;
                while (pos > 0 && hkeys_arr[pos - 1] == hkey) pos--;

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);
                    object val = sequence.GetByOffset(offset);
                    if (val == null) break;
                    var key = keyFunc(val);
                    if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(val, offset))
                        result.Add(offset);
                    pos++;
                }

                return result;
            }

            long first = GetFirstNom(hkey);
            if (first == -1) return result;
            for (long nom = first; nom < hkeys.Count(); nom++)
            {
                int currentHash = (int)hkeys.GetByIndex(nom);
                if (currentHash != hkey) break;
                long offset = (long)offsets.GetByIndex(nom);
                object val = sequence.GetByOffset(offset);
                if (val == null) break;
                var key = keyFunc(val);
                if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(val, offset))
                    result.Add(offset);
            }

            return result;
        }

        private static double Measure(Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
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
