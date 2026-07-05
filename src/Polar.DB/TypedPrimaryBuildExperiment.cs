using System.Diagnostics;
using System.Reflection;
using Polar.DB;

namespace Polar.Universal;

internal readonly struct PrimaryBuildEntry<TKey>
    where TKey : struct
{
    public PrimaryBuildEntry(int hashKey, TKey key, long offset, bool isEmpty)
    {
        HashKey = hashKey;
        Key = key;
        Offset = offset;
        IsEmpty = isEmpty;
    }

    public int HashKey { get; }
    public TKey Key { get; }
    public long Offset { get; }
    public bool IsEmpty { get; }
}

internal sealed class PrimaryBuildEntryComparer<TKey> : IComparer<PrimaryBuildEntry<TKey>>
    where TKey : struct, IComparable<TKey>
{
    public static readonly PrimaryBuildEntryComparer<TKey> Instance = new();

    public int Compare(PrimaryBuildEntry<TKey> left, PrimaryBuildEntry<TKey> right)
    {
        var hashComparison = left.HashKey.CompareTo(right.HashKey);
        if (hashComparison != 0) return hashComparison;

        var keyComparison = left.Key.CompareTo(right.Key);
        if (keyComparison != 0) return keyComparison;

        return left.Offset.CompareTo(right.Offset);
    }
}

internal interface ILoadedTypedPrimaryBuild
{
    void Build(UKeyIndex index);
}

internal sealed class LoadedTypedPrimaryBuild<TKey> : ILoadedTypedPrimaryBuild
    where TKey : struct, IComparable<TKey>, IEquatable<TKey>
{
    private PrimaryBuildEntry<TKey>[] entries;

    public LoadedTypedPrimaryBuild(PrimaryBuildEntry<TKey>[] entries)
    {
        this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public void Build(UKeyIndex index)
    {
        TypedPrimaryBuildExperiment.Build(index, entries);
        entries = Array.Empty<PrimaryBuildEntry<TKey>>();
    }
}

internal static class TypedPrimaryBuildExperiment
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo HashKeysField = RequireField(typeof(UKeyIndex), "hkeys");
    private static readonly FieldInfo OffsetsField = RequireField(typeof(UKeyIndex), "offsets");
    private static readonly FieldInfo HashKeysArrayField = RequireField(typeof(UKeyIndex), "hkeys_arr");
    private static readonly FieldInfo OriginalOffsetsField = RequireField(typeof(UKeyIndex), "original_offsets_set");
    private static readonly FieldInfo DynamicOffsetsField = RequireField(typeof(UKeyIndex), "keyoff_dic");
    private static readonly FieldInfo HasBuiltSnapshotField = RequireField(typeof(UKeyIndex), "hasBuiltSnapshot");
    private static readonly FieldInfo KeysInMemoryField = RequireField(typeof(UKeyIndex), "keysinmemory");
    private static readonly PropertyInfo LastBuildProfileProperty =
        typeof(UKeyIndex).GetProperty(nameof(UKeyIndex.LastBuildProfile))
        ?? throw new MissingMemberException(typeof(UKeyIndex).FullName, nameof(UKeyIndex.LastBuildProfile));

    public static void Build<TKey>(UKeyIndex index, PrimaryBuildEntry<TKey>[] entries)
        where TKey : struct, IComparable<TKey>, IEquatable<TKey>
    {
        if (index == null) throw new ArgumentNullException(nameof(index));
        if (entries == null) throw new ArgumentNullException(nameof(entries));

        var totalWatch = Stopwatch.StartNew();
        var sortMs = Measure(() =>
        {
            if (entries.Length > 1)
                Array.Sort(entries, PrimaryBuildEntryComparer<TKey>.Instance);
        });

        int[] hashKeys = Array.Empty<int>();
        long[] offsets = Array.Empty<long>();
        var toArrayMs = Measure(() =>
        {
            var liveCount = CompactLatestLiveEntries(entries);
            hashKeys = new int[liveCount];
            offsets = new long[liveCount];

            for (var i = 0; i < liveCount; i++)
            {
                hashKeys[i] = entries[i].HashKey;
                offsets[i] = entries[i].Offset;
            }
        });

        var hashKeyStore = (UniversalSequenceBase)(HashKeysField.GetValue(index)
            ?? throw new InvalidOperationException("Primary hash-key store is not available."));
        var offsetStore = (UniversalSequenceBase)(OffsetsField.GetValue(index)
            ?? throw new InvalidOperationException("Primary offset store is not available."));
        var keysInMemory = (bool)(KeysInMemoryField.GetValue(index)
            ?? throw new InvalidOperationException("Primary index memory mode is not available."));

        var writeHashKeysMs = Measure(() => hashKeyStore.ReplaceWithFixedInt32Array(hashKeys));
        var writeOffsetsMs = Measure(() => offsetStore.ReplaceWithFixedInt64Array(offsets));

        HashKeysArrayField.SetValue(index, keysInMemory ? hashKeys : null);
        OriginalOffsetsField.SetValue(index, keysInMemory ? new HashSet<long>(offsets) : null);
        ((Dictionary<IComparable, long>)(DynamicOffsetsField.GetValue(index)
            ?? throw new InvalidOperationException("Dynamic primary offsets are not available."))).Clear();
        HasBuiltSnapshotField.SetValue(index, true);

        totalWatch.Stop();
        LastBuildProfileProperty.SetValue(index, new UIndexBuildProfile(
            scanMs: 0.0,
            toArrayMs,
            sortMs,
            writeHashKeysMs,
            writeOffsetsMs,
            gcMs: 0.0,
            totalWatch.Elapsed.TotalMilliseconds));
    }

    private static int CompactLatestLiveEntries<TKey>(PrimaryBuildEntry<TKey>[] entries)
        where TKey : struct, IEquatable<TKey>
    {
        var liveCount = 0;
        var index = 0;

        while (index < entries.Length)
        {
            var latest = entries[index++];
            while (index < entries.Length &&
                   latest.HashKey == entries[index].HashKey &&
                   latest.Key.Equals(entries[index].Key))
            {
                latest = entries[index++];
            }

            if (!latest.IsEmpty)
                entries[liveCount++] = latest;
        }

        return liveCount;
    }

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
