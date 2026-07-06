using System.Diagnostics;
using System.Reflection;
using Polar.DB;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal readonly struct Int64TypedPrimaryBuildProbeEntry
{
    public Int64TypedPrimaryBuildProbeEntry(int hashKey, long key, long offset)
    {
        HashKey = hashKey;
        Key = key;
        Offset = offset;
    }

    public int HashKey { get; }
    public long Key { get; }
    public long Offset { get; }
}

internal sealed class Int64TypedPrimaryBuildProbeEntryComparer : IComparer<Int64TypedPrimaryBuildProbeEntry>
{
    public static readonly Int64TypedPrimaryBuildProbeEntryComparer Instance = new();

    public int Compare(Int64TypedPrimaryBuildProbeEntry left, Int64TypedPrimaryBuildProbeEntry right)
    {
        var hashComparison = left.HashKey.CompareTo(right.HashKey);
        if (hashComparison != 0) return hashComparison;

        var keyComparison = left.Key.CompareTo(right.Key);
        if (keyComparison != 0) return keyComparison;

        return left.Offset.CompareTo(right.Offset);
    }
}

internal static class Int64TypedPrimaryBuildProbe
{
    private const long HeaderSize = sizeof(long);
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo PrimaryKeyIndexField = RequireField(typeof(USequence), "primaryKeyIndex");
    private static readonly FieldInfo HashKeysField = RequireField(typeof(UKeyIndex), "hkeys");
    private static readonly FieldInfo OffsetsField = RequireField(typeof(UKeyIndex), "offsets");
    private static readonly FieldInfo HashKeysArrayField = RequireField(typeof(UKeyIndex), "hkeys_arr");
    private static readonly FieldInfo OriginalOffsetsField = RequireField(typeof(UKeyIndex), "original_offsets_set");
    private static readonly FieldInfo DynamicOffsetsField = RequireField(typeof(UKeyIndex), "keyoff_dic");
    private static readonly FieldInfo HasBuiltSnapshotField = RequireField(typeof(UKeyIndex), "hasBuiltSnapshot");
    private static readonly PropertyInfo LastBuildProfileProperty =
        typeof(UKeyIndex).GetProperty(nameof(UKeyIndex.LastBuildProfile))
        ?? throw new MissingMemberException(typeof(UKeyIndex).FullName, nameof(UKeyIndex.LastBuildProfile));

    public static Int64TypedPrimaryBuildProbeEntry[] Load(USequence sequence, long[] values)
    {
        if (sequence == null) throw new ArgumentNullException(nameof(sequence));
        if (values == null) throw new ArgumentNullException(nameof(values));

        sequence.LoadFixedInt64StorageOnlyForBenchmark(values);

        var entries = new Int64TypedPrimaryBuildProbeEntry[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            entries[i] = new Int64TypedPrimaryBuildProbeEntry(
                BenchmarkChecksum.StableHash(value), value, HeaderSize + i * sizeof(long));
        }

        return entries;
    }

    public static UIndexBuildProfile Build(USequence sequence, Int64TypedPrimaryBuildProbeEntry[] entries)
    {
        if (sequence == null) throw new ArgumentNullException(nameof(sequence));
        if (entries == null) throw new ArgumentNullException(nameof(entries));

        var totalWatch = Stopwatch.StartNew();
        var sortMs = Measure(() =>
        {
            if (entries.Length > 1)
                Array.Sort(entries, Int64TypedPrimaryBuildProbeEntryComparer.Instance);
        });

        int[] hashKeys = Array.Empty<int>();
        long[] offsets = Array.Empty<long>();
        var toArrayMs = Measure(() =>
        {
            var liveCount = CompactLatestEntries(entries);
            hashKeys = new int[liveCount];
            offsets = new long[liveCount];

            for (var i = 0; i < liveCount; i++)
            {
                hashKeys[i] = entries[i].HashKey;
                offsets[i] = entries[i].Offset;
            }
        });

        var index = (UKeyIndex)(PrimaryKeyIndexField.GetValue(sequence)
            ?? throw new InvalidOperationException("Primary key index is not available."));
        var hashKeyStore = (UniversalSequenceBase)(HashKeysField.GetValue(index)
            ?? throw new InvalidOperationException("Primary hash-key store is not available."));
        var offsetStore = (UniversalSequenceBase)(OffsetsField.GetValue(index)
            ?? throw new InvalidOperationException("Primary offset store is not available."));

        var writeHashKeysMs = Measure(() => hashKeyStore.ReplaceWithFixedInt32Array(hashKeys));
        var writeOffsetsMs = Measure(() => offsetStore.ReplaceWithFixedInt64Array(offsets));

        HashKeysArrayField.SetValue(index, hashKeys);
        OriginalOffsetsField.SetValue(index, new HashSet<long>(offsets));
        ((Dictionary<IComparable, long>)(DynamicOffsetsField.GetValue(index)
            ?? throw new InvalidOperationException("Dynamic primary offsets are not available."))).Clear();
        HasBuiltSnapshotField.SetValue(index, true);

        totalWatch.Stop();
        var profile = new UIndexBuildProfile(
            scanMs: 0.0,
            toArrayMs,
            sortMs,
            writeHashKeysMs,
            writeOffsetsMs,
            gcMs: 0.0,
            totalWatch.Elapsed.TotalMilliseconds);
        LastBuildProfileProperty.SetValue(index, profile);
        return profile;
    }

    private static int CompactLatestEntries(Int64TypedPrimaryBuildProbeEntry[] entries)
    {
        var liveCount = 0;
        var index = 0;

        while (index < entries.Length)
        {
            var latest = entries[index++];
            while (index < entries.Length && IsSameLogicalKey(latest, entries[index]))
                latest = entries[index++];

            entries[liveCount++] = latest;
        }

        return liveCount;
    }

    private static bool IsSameLogicalKey(
        Int64TypedPrimaryBuildProbeEntry left,
        Int64TypedPrimaryBuildProbeEntry right) =>
        left.HashKey == right.HashKey && left.Key == right.Key;

    private static FieldInfo RequireField(Type type, string name) =>
        type.GetField(name, InstancePrivate)
        ?? throw new MissingFieldException(type.FullName, name);

    private static double Measure(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
}
