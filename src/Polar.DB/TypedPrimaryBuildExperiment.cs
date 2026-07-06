using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Polar.DB;

namespace Polar.Universal;

internal readonly struct PrimaryBuildEntry<TKey>
    where TKey : struct
{
    private const long EmptyFlag = long.MinValue;
    private const long OffsetMask = long.MaxValue;

    private readonly long offsetAndFlags;

    public PrimaryBuildEntry(int hashKey, TKey key, long offset, bool isEmpty)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

        HashKey = hashKey;
        Key = key;
        offsetAndFlags = isEmpty ? offset | EmptyFlag : offset;
    }

    public int HashKey { get; }
    public TKey Key { get; }
    public long Offset => offsetAndFlags & OffsetMask;
    public bool IsEmpty => offsetAndFlags < 0;
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
    private const long HeaderSize = sizeof(long);
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo HashKeysField = RequireField(typeof(UKeyIndex), "hkeys");
    private static readonly FieldInfo OffsetsField = RequireField(typeof(UKeyIndex), "offsets");
    private static readonly FieldInfo HashKeysArrayField = RequireField(typeof(UKeyIndex), "hkeys_arr");
    private static readonly FieldInfo OriginalOffsetsField = RequireField(typeof(UKeyIndex), "original_offsets_set");
    private static readonly FieldInfo DynamicOffsetsField = RequireField(typeof(UKeyIndex), "keyoff_dic");
    private static readonly FieldInfo HasBuiltSnapshotField = RequireField(typeof(UKeyIndex), "hasBuiltSnapshot");
    private static readonly FieldInfo KeysInMemoryField = RequireField(typeof(UKeyIndex), "keysinmemory");
    private static readonly FieldInfo ElementSizeField = RequireField(typeof(UniversalSequenceBase), "elem_size");
    private static readonly FieldInfo ElementCountField = RequireField(typeof(UniversalSequenceBase), "nelements");
    private static readonly FieldInfo AppendOffsetField = RequireField(typeof(UniversalSequenceBase), "append_offset");
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

        var writeHashKeysMs = Measure(() => ReplaceWithFixedArrayDirect(hashKeyStore, hashKeys, sizeof(int)));
        var writeOffsetsMs = Measure(() => ReplaceWithFixedArrayDirect(offsetStore, offsets, sizeof(long)));

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

    private static void ReplaceWithFixedArrayDirect<T>(
        UniversalSequenceBase sequence,
        T[] values,
        int expectedElementSize)
        where T : struct
    {
        var elementSize = (int)(ElementSizeField.GetValue(sequence)
            ?? throw new InvalidOperationException("Fixed element size is not available."));
        if (elementSize != expectedElementSize)
            throw new InvalidOperationException(
                $"Direct fixed-array write requires element size {expectedElementSize}, actual {elementSize}.");

        var payload = MemoryMarshal.AsBytes(values.AsSpan());
        var media = sequence.Media;
        media.Position = 0L;
        media.SetLength(0L);

        Span<byte> header = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(header, values.LongLength);
        media.Write(header);
        media.Write(payload);

        var appendOffset = HeaderSize + payload.Length;
        ElementCountField.SetValue(sequence, values.LongLength);
        AppendOffsetField.SetValue(sequence, appendOffset);
        media.Position = appendOffset;
        media.Flush();
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
