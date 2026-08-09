using Polar.DB;

namespace Polar.Universal
{
    public class UKeyIndex : IDisposable
    {
        private readonly USequence sequence;
        private IPrimaryKeyAccessor? _primaryKeyAccessor;

        private IPrimaryKeyAccessor PrimaryKeyAccessor =>
            _primaryKeyAccessor ?? throw new InvalidOperationException("Primary key accessor is not configured.");
        private readonly UniversalSequenceBase hkeys;
        private readonly UniversalSequenceBase offsets;
        private readonly Dictionary<IComparable, long> keyoff_dic;
        internal bool ElementChanged(IComparable key) => keyoff_dic.ContainsKey(key);
        private readonly bool keysinmemory;
        private int[]? hkeys_arr;
        private long[]? offsets_arr;
        private HashSet<long>? stale_offsets_set;
        private HashSet<long>? legacy_original_offsets_set;
        private bool snapshotOriginalityKnown;
        private bool hasBuiltSnapshot;
        private bool disposed;

        public UIndexBuildProfile LastBuildProfile { get; private set; } = UIndexBuildProfile.Empty;

        public UKeyIndex(Func<Stream> streamGen, USequence sequence,
            Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool keysinmemory = true)
            : this(streamGen, sequence, keysinmemory)
        {
            SetPrimaryKeyAccessor(new DelegatePrimaryKeyAccessor(keyFunc, hashOfKey));
        }

        internal UKeyIndex(Func<Stream> streamGen, USequence sequence, bool keysinmemory = true)
        {
            this.sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            this.keysinmemory = keysinmemory;
            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            keyoff_dic = new Dictionary<IComparable, long>();
        }

        internal bool HasBuiltSnapshot => hasBuiltSnapshot;

        internal long[] GetStaleOffsetsSnapshot() =>
            stale_offsets_set == null || stale_offsets_set.Count == 0
                ? Array.Empty<long>()
                : stale_offsets_set.ToArray();

        internal void SetPrimaryKeyAccessor(IPrimaryKeyAccessor accessor)
        {
            if (_primaryKeyAccessor != null)
                throw new InvalidOperationException("Primary key accessor is already configured.");
            _primaryKeyAccessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        }

        public void OnAppendElement(object element, long offset)
        {
            var key = PrimaryKeyAccessor.GetKey(element);
            keyoff_dic[key] = offset;
        }

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            offsets_arr = null;
            stale_offsets_set = null;
            legacy_original_offsets_set = null;
            snapshotOriginalityKnown = false;
            keyoff_dic.Clear();
            hasBuiltSnapshot = false;
            LastBuildProfile = UIndexBuildProfile.Empty;
        }

        public void Flush()
        {
            hkeys.Flush();
            offsets.Flush();
        }

        public void Close() => Dispose();

        public void Refresh() => Refresh(snapshotBuilt: false, staleOffsets: null, staleMetadataKnown: false);

        internal void Refresh(bool snapshotBuilt, IReadOnlyCollection<long>? staleOffsets, bool staleMetadataKnown)
        {
            hkeys.Refresh();
            offsets.Refresh();
            var persistedKeyCount = hkeys.Count();
            if (persistedKeyCount != offsets.Count())
                throw new InvalidDataException("Primary-key hash and offset sequence lengths differ.");

            if (keysinmemory)
            {
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
                offsets_arr = offsets.ElementValues().Cast<long>().ToArray();
                if (hkeys_arr.LongLength != offsets_arr.LongLength)
                    throw new InvalidDataException("In-memory primary-key hash and offset array lengths differ.");
            }
            else
            {
                hkeys_arr = null;
                offsets_arr = null;
            }

            stale_offsets_set = null;
            legacy_original_offsets_set = null;
            snapshotOriginalityKnown = false;

            if (staleMetadataKnown)
            {
                if (staleOffsets != null && staleOffsets.Count != 0)
                    stale_offsets_set = new HashSet<long>(staleOffsets);
                snapshotOriginalityKnown = snapshotBuilt;
            }
            else if (keysinmemory && persistedKeyCount != 0)
            {
                // Backward compatibility for state files written before stale-offset metadata existed.
                // New snapshots never allocate this O(N) set. The offset array is the same
                // compact static snapshot used by lookups, so it is also safe as the legacy source.
                legacy_original_offsets_set = new HashSet<long>(offsets_arr!);
            }

            hasBuiltSnapshot = snapshotBuilt || persistedKeyCount > 0;
        }

        public void Build()
        {
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
            var capacity = ArrayHelper.GetBuildCapacityUpperBound(sequence.Count());
            var entries = capacity == 0 ? Array.Empty<BuildEntry>() : new BuildEntry[capacity];
            var entryCount = 0;

            var scanMs = Measure(() =>
            {
                sequence.ScanPhysical((off, obj) =>
                {
                    var key = PrimaryKeyAccessor.GetKey(obj);
                    entries[entryCount++] = new BuildEntry(PrimaryKeyAccessor.Hash(key), key, off, sequence.IsEmpty(obj));
                    return true;
                });
            });

            BuildFromEntries(entries, entryCount, scanMs, totalWatch);
        }

        internal void BuildFromLoadedEntries(BuildEntry[] entries, int entryCount)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entryCount < 0 || entryCount > entries.Length) throw new ArgumentOutOfRangeException(nameof(entryCount));

            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
            BuildFromEntries(entries, entryCount, scanMs: 0.0, totalWatch);
        }

        private void BuildFromEntries(BuildEntry[] entries, int entryCount, double scanMs, System.Diagnostics.Stopwatch totalWatch)
        {
            var toArrayMs = 0.0;
            var sortMs = 0.0;
            var writeHashKeysMs = 0.0;
            var writeOffsetsMs = 0.0;
            var gcMs = 0.0;

            sortMs = Measure(() =>
            {
                if (entryCount > 1)
                    Array.Sort(entries, 0, entryCount, BuildEntryComparer.Instance);
            });

            long[] offsetsArray = Array.Empty<long>();
            toArrayMs = Measure(() =>
            {
                var liveCount = CompactLatestLiveEntries(entries, entryCount, out var staleOffsets);
                hkeys_arr = new int[liveCount];
                offsetsArray = new long[liveCount];

                for (var i = 0; i < liveCount; i++)
                {
                    hkeys_arr[i] = entries[i].HashKey;
                    offsetsArray[i] = entries[i].Offset;
                }

                stale_offsets_set = staleOffsets.Length == 0 ? null : new HashSet<long>(staleOffsets);
                legacy_original_offsets_set = null;
                snapshotOriginalityKnown = true;
                entries = Array.Empty<BuildEntry>();
            });

            writeHashKeysMs = Measure(() =>
            {
                hkeys.ReplaceWithFixedInt32Array(hkeys_arr!);
                if (!keysinmemory) hkeys_arr = null;
            });

            writeOffsetsMs = Measure(() => offsets.ReplaceWithFixedInt64Array(offsetsArray));
            offsets_arr = keysinmemory ? offsetsArray : null;

            keyoff_dic.Clear();
            hasBuiltSnapshot = true;
            offsetsArray = Array.Empty<long>();
            totalWatch.Stop();

            LastBuildProfile = new UIndexBuildProfile(
                scanMs, toArrayMs, sortMs, writeHashKeysMs, writeOffsetsMs,
                gcMs, totalWatch.Elapsed.TotalMilliseconds);
        }

        public object? GetByKey(IComparable keysample)
        {
            if (keyoff_dic.TryGetValue(keysample, out long off))
            {
                var dynamicValue = sequence.GetByOffset(off);
                return dynamicValue != null && !sequence.IsEmpty(dynamicValue)
                    ? dynamicValue
                    : null;
            }

            return TryGetIndexedValueByKey(keysample, out var indexedValue) ? indexedValue : null;
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
            if (keyoff_dic.TryGetValue(key, out long dynamicOffset))
                return dynamicOffset == offset;

            if (snapshotOriginalityKnown)
                return stale_offsets_set == null || !stale_offsets_set.Contains(offset);

            if (legacy_original_offsets_set != null)
                return legacy_original_offsets_set.Contains(offset);

            if (TryGetIndexedOffsetByKey(key, out var indexedOffset))
                return indexedOffset == offset;

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

            var key = PrimaryKeyAccessor.GetKey(value);
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
                var key = PrimaryKeyAccessor.GetKey(value);
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
            int hkey = PrimaryKeyAccessor.Hash(keysample);
            var memoryOffsets = offsets_arr;

            if (hkeys_arr != null && memoryOffsets != null)
            {
                int pos = LowerBound(hkeys_arr, hkey);

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = memoryOffsets[pos];
                    object val = sequence.GetByOffset(offset);
                    if (val == null) break;
                    var key = PrimaryKeyAccessor.GetKey(val);
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
                var key = PrimaryKeyAccessor.GetKey(val);
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
            value = null!;
            return false;
        }

        private bool TryGetIndexedValueAndOffsetByKey(IComparable keysample, out object value, out long offset)
        {
            int hkey = PrimaryKeyAccessor.Hash(keysample);
            var memoryOffsets = offsets_arr;

            if (hkeys_arr != null && memoryOffsets != null)
            {
                int pos = LowerBound(hkeys_arr, hkey);
                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    var candidateOffset = memoryOffsets[pos];
                    var candidateValue = sequence.GetByOffset(candidateOffset);
                    if (candidateValue != null)
                    {
                        var candidateKey = PrimaryKeyAccessor.GetKey(candidateValue);
                        if (candidateKey.CompareTo(keysample) == 0)
                        {
                            value = candidateValue;
                            offset = candidateOffset;
                            return true;
                        }
                    }

                    pos++;
                }

                value = null!;
                offset = default;
                return false;
            }

            long count = hkeys.Count();
            long first = GetFirstNom(hkey);
            if (first == -1)
            {
                value = null!;
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

                var candidateKey = PrimaryKeyAccessor.GetKey(candidateValue);
                if (candidateKey.CompareTo(keysample) == 0)
                {
                    value = candidateValue;
                    offset = candidateOffset;
                    return true;
                }
            }

            value = null!;
            offset = default;
            return false;
        }

        private static int CompactLatestLiveEntries(BuildEntry[] entries, int entryCount, out long[] staleOffsets)
        {
            var liveCount = 0;
            var index = 0;
            List<long>? stale = null;

            while (index < entryCount)
            {
                var groupStart = index;
                var latest = entries[index++];
                while (index < entryCount && IsSameLogicalKey(latest, entries[index]))
                    latest = entries[index++];

                for (var i = groupStart; i < index - 1; i++)
                {
                    stale ??= new List<long>();
                    stale.Add(entries[i].Offset);
                }

                if (latest.IsEmpty)
                {
                    stale ??= new List<long>();
                    stale.Add(latest.Offset);
                }
                else
                {
                    entries[liveCount++] = latest;
                }
            }

            staleOffsets = stale == null ? Array.Empty<long>() : stale.ToArray();
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
