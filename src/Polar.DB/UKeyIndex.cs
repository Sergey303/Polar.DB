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
        private HashSet<long> original_offsets_set = null;
        private bool hasBuiltSnapshot;
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
            keyoff_dic[key] = offset;
        }

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            original_offsets_set = null;
            keyoff_dic.Clear();
            hasBuiltSnapshot = false;
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
            var persistedKeyCount = hkeys.Count();
            if (keysinmemory)
            {
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
                original_offsets_set = new HashSet<long>(offsets.ElementValues().Cast<long>());
            }
            else
            {
                hkeys_arr = null;
                original_offsets_set = null;
            }

            hasBuiltSnapshot = persistedKeyCount > 0;
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

            var capacity = ArrayHelper.GetBuildCapacityUpperBound(sequence.Count());
            var entries = capacity == 0 ? Array.Empty<BuildEntry>() : new BuildEntry[capacity];
            var entryCount = 0;

            scanMs = Measure(() =>
            {
                sequence.ScanPhysical((off, obj) =>
                {
                    if (entryCount == entries.Length)
                        Array.Resize(ref entries, ArrayHelper.GrowCapacity(entries.Length));

                    var key = keyFunc(obj);
                    entries[entryCount++] = new BuildEntry(hashOfKey(key), key, off, sequence.IsEmpty(obj));
                    return true;
                });
            });

            sortMs = Measure(() =>
            {
                if (entryCount > 1)
                    Array.Sort(entries, 0, entryCount, BuildEntryComparer.Instance);
            });

            long[] offsets_arr = Array.Empty<long>();
            toArrayMs = Measure(() =>
            {
                var liveCount = CompactLatestLiveEntries(entries, entryCount);
                hkeys_arr = new int[liveCount];
                offsets_arr = new long[liveCount];

                for (var i = 0; i < liveCount; i++)
                {
                    hkeys_arr[i] = entries[i].HashKey;
                    offsets_arr[i] = entries[i].Offset;
                }

                entries = Array.Empty<BuildEntry>();
            });

            writeHashKeysMs = Measure(() =>
            {
                hkeys.ReplaceWithFixedInt32Array(hkeys_arr);
                if (!keysinmemory) hkeys_arr = null;
            });

            writeOffsetsMs = Measure(() =>
            {
                offsets.ReplaceWithFixedInt64Array(offsets_arr);
            });

            original_offsets_set = keysinmemory ? new HashSet<long>(offsets_arr) : null;
            keyoff_dic.Clear();
            hasBuiltSnapshot = true;
            offsets_arr = null;
            totalWatch.Stop();

            LastBuildProfile = new UIndexBuildProfile(
                scanMs, toArrayMs, sortMs, writeHashKeysMs, writeOffsetsMs,
                gcMs, totalWatch.Elapsed.TotalMilliseconds);
        }

        public object GetByKey(IComparable keysample)
        {
            if (keyoff_dic.TryGetValue(keysample, out long off))
            {
                var dynamicValue = sequence.GetByOffset(off);
                return dynamicValue != null && !sequence.IsEmpty(dynamicValue)
                    ? dynamicValue
                    : null;
            }

            if (TryGetIndexedValueByKey(keysample, out var indexedValue)) return indexedValue;
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
            if (original_offsets_set != null) return original_offsets_set.Contains(offset);
            if (TryGetIndexedOffsetByKey(key, out var indexedOffset)) return indexedOffset == offset;
            return !hasBuiltSnapshot;
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
                int pos = LowerBound(hkeys_arr, hkey);

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);
                    object val = sequence.GetByOffset(offset);
                    if (val == null) break;
                    var key = keyFunc(val);
                    if (key.CompareTo(keysample) == 0)
                        result.Add(offset);
                    pos++;
                }

                return result;
            }

            long count = hkeys.Count();
            long first = GetFirstNom(hkey);
            if (first == -1) return result;
            for (long nom = first; nom < count; nom++)
            {
                int currentHash = (int)hkeys.GetByIndex(nom);
                if (currentHash != hkey) break;
                long offset = (long)offsets.GetByIndex(nom);
                object val = sequence.GetByOffset(offset);
                if (val == null) break;
                var key = keyFunc(val);
                if (key.CompareTo(keysample) == 0)
                    result.Add(offset);
            }

            return result;
        }

        private bool TryGetIndexedOffsetByKey(IComparable keysample, out long offset)
        {
            if (TryGetIndexedValueAndOffsetByKey(keysample, out _, out offset)) return true;
            offset = default;
            return false;
        }

        private bool TryGetIndexedValueByKey(IComparable keysample, out object value)
        {
            if (TryGetIndexedValueAndOffsetByKey(keysample, out value, out _)) return true;
            value = null;
            return false;
        }

        private bool TryGetIndexedValueAndOffsetByKey(IComparable keysample, out object value, out long offset)
        {
            int hkey = hashOfKey(keysample);

            if (hkeys_arr != null)
            {
                int pos = LowerBound(hkeys_arr, hkey);
                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    var candidateOffset = (long)offsets.GetByIndex(pos);
                    var candidateValue = sequence.GetByOffset(candidateOffset);
                    if (candidateValue != null)
                    {
                        var candidateKey = keyFunc(candidateValue);
                        if (candidateKey.CompareTo(keysample) == 0)
                        {
                            value = candidateValue;
                            offset = candidateOffset;
                            return true;
                        }
                    }

                    pos++;
                }

                value = null;
                offset = default;
                return false;
            }

            long count = hkeys.Count();
            long first = GetFirstNom(hkey);
            if (first == -1)
            {
                value = null;
                offset = default;
                return false;
            }

            for (long nom = first; nom < count; nom++)
            {
                int currentHash = (int)hkeys.GetByIndex(nom);
                if (currentHash != hkey) break;

                var candidateOffset = (long)offsets.GetByIndex(nom);
                var candidateValue = sequence.GetByOffset(candidateOffset);
                if (candidateValue == null) continue;

                var candidateKey = keyFunc(candidateValue);
                if (candidateKey.CompareTo(keysample) == 0)
                {
                    value = candidateValue;
                    offset = candidateOffset;
                    return true;
                }
            }

            value = null;
            offset = default;
            return false;
        }

        private static int CompactLatestLiveEntries(BuildEntry[] entries, int entryCount)
        {
            var liveCount = 0;
            var index = 0;

            while (index < entryCount)
            {
                var latest = entries[index++];
                while (index < entryCount && IsSameLogicalKey(latest, entries[index]))
                    latest = entries[index++];

                if (!latest.IsEmpty)
                    entries[liveCount++] = latest;
            }

            return liveCount;
        }

        private static bool IsSameLogicalKey(BuildEntry left, BuildEntry right) =>
            left.HashKey == right.HashKey && left.Key.CompareTo(right.Key) == 0;

        private static int LowerBound(int[] values, int value)
        {
            int left = 0;
            int right = values.Length;

            while (left < right)
            {
                int middle = left + (right - left) / 2;
                if (values[middle] < value) left = middle + 1;
                else right = middle;
            }

            return left;
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