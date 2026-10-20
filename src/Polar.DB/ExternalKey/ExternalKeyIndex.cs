using Polar.Universal;

namespace Polar.DB.ExternalKey;

public sealed class ExternalKeyIndex<T> : IExternalKeyIndex<T>
    where T : IComparable<T>
{
    private readonly USequence _sequence;
    private readonly Func<object, IEnumerable<T>> _keysFunc;
    private readonly IComparer<T> _comparer;
    private readonly UniversalSequenceBase _keys;
    private readonly UniversalSequenceBase _offsets;
    private readonly bool _legacyGuidStringStorage;
    private readonly string? _rebuildMarkerPath;
    private readonly List<ExternalKeyIndexEntry<T>> _dynamic = new();
    private ExternalKeyIndexSnapshot<T> _snapshot = ExternalKeyIndexSnapshot<T>.Empty;
    private long _revision;
    private bool _needsRebuild;
    private bool _disposed;

    public ExternalKeyIndex(
        Func<Stream> streamGen,
        USequence sequence,
        Func<object, IEnumerable<T>> keysFunc,
        IComparer<T>? comparer = null)
    {
        _ = streamGen ?? throw new ArgumentNullException(nameof(streamGen));
        _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _keysFunc = keysFunc ?? throw new ArgumentNullException(nameof(keysFunc));
        _comparer = comparer ?? Comparer<T>.Default;

        Stream keyStream = streamGen();
        _rebuildMarkerPath = typeof(T) == typeof(Guid) && keyStream is FileStream file
            ? file.Name + ".rebuild-required"
            : null;
        _needsRebuild = _rebuildMarkerPath != null && File.Exists(_rebuildMarkerPath);
        try
        {
            _legacyGuidStringStorage = ExternalKeyIndexKeyCodec<T>.IsLegacyGuidStringStorage(keyStream);
        }
        catch (InvalidDataException) when (ExternalGuidIndexRecovery.CanRebuildKeyStream<T>(keyStream))
        {
            // Only a derived index is reset; the source sequence remains untouched.
            ExternalGuidIndexRecovery.Reset(keyStream);
            _needsRebuild = true;
        }

        _keys = UniversalSequenceBase.OpenIndexSequence(
            ExternalKeyIndexKeyCodec<T>.GetStorageType(_legacyGuidStringStorage),
            keyStream);
        Stream offsetStream = streamGen();
        if (typeof(T) == typeof(Guid) && ExternalGuidIndexRecovery.HasInterruptedOffsets(offsetStream))
        {
            ExternalGuidIndexRecovery.Reset(offsetStream);
            _needsRebuild = true;
        }
        _offsets = UniversalSequenceBase.OpenIndexSequence(
            new PType(PTypeEnumeration.longinteger), offsetStream);
    }

    public void Clear()
    {
        MarkRebuildRequired();
        _keys.Clear();
        _offsets.Clear();
        _dynamic.Clear();
        _snapshot = ExternalKeyIndexSnapshot<T>.Empty;
        _revision++;
    }

    public void Flush()
    {
        _keys.Flush();
        _offsets.Flush();
    }

    public void Close()
    {
        Dispose();
    }

    public void Refresh()
    {
        if (_needsRebuild || (typeof(T) == typeof(Guid) &&
            (_keys.Count() != _offsets.Count() || (_keys.Count() == 0 && _sequence.Count() > 0))))
        {
            Build();
            return;
        }

        _keys.Refresh();
        _offsets.Refresh();
        _snapshot = ExternalKeyIndexCompaction<T>.ReadSnapshot(
            _keys,
            _offsets,
            _legacyGuidStringStorage);
        _dynamic.Clear();
        _revision++;
    }

    public void Build()
    {
        var snapshot = ExternalKeyIndexCompaction<T>.BuildSnapshot(
            _sequence, _keysFunc, _comparer, CancellationToken.None);

        MarkRebuildRequired();
        ExternalKeyIndexCompaction<T>.WriteSnapshot(
            _keys,
            _offsets,
            snapshot,
            _legacyGuidStringStorage,
            CancellationToken.None);
        PublishSnapshot(snapshot, long.MaxValue);
        _needsRebuild = false;
        if (_rebuildMarkerPath != null) File.Delete(_rebuildMarkerPath);
    }

    public void OnAppendElement(object element, long offset)
    {
        IComparable primary = _sequence.GetPrimaryKey(element);
        long revision = ++_revision;

        _dynamic.RemoveAll(entry => Equals(entry.Primary, primary));

        foreach (T key in GetKeys(element))
            _dynamic.Add(new ExternalKeyIndexEntry<T>(primary, key, offset, revision));
    }

    public IEnumerable<object> GetManyByValue(T key) => GetManyByValue(key, null);

    public IEnumerable<object> GetManyByValue(T key, Func<object, bool>? elementFilter)
    {
        if (_needsRebuild) Build();
        ExternalKeyIndexSnapshot<T> snapshot = _snapshot;
        ExternalKeyIndexEntry<T>[] dynamicEntries = _dynamic.Count == 0
            ? Array.Empty<ExternalKeyIndexEntry<T>>()
            : _dynamic.ToArray();

        var emittedOffsets = new HashSet<long>();
        foreach (ExternalKeyIndexEntry<T> entry in dynamicEntries)
        {
            if (!KeyEquals(entry.Key, key)) continue;
            object? value = TryReadDynamic(entry, elementFilter, emittedOffsets);
            if (value != null) yield return value;
        }

        int pos = ExternalKeyIndexSearch.FindFirstEqual(snapshot.Keys, key, _comparer);
        for (int i = pos; i >= 0 && i < snapshot.Keys.Length && KeyEquals(snapshot.Keys[i], key); i++)
        {
            object? value = TryReadStatic(snapshot.Offsets[i], key, elementFilter, emittedOffsets);
            if (value != null) yield return value;
        }
    }

    IEnumerable<object> IExternalKeyIndex.GetManyByValue(IComparable value) =>
        GetManyByValue(ExternalKeyIndexKeyCodec<T>.Cast(value));

    private object? TryReadStatic(long offset, T key, Func<object, bool>? filter, HashSet<long> emitted)
    {
        object element = _sequence.GetByOffset(offset);
        if (!_sequence.IsOriginalAndNotEmpty(element, offset)) return null;
        if (filter != null && !filter(element)) return null;
        if (filter == null && !HasKey(element, key)) return null;
        return emitted.Add(offset) ? element : null;
    }

    private object? TryReadDynamic(ExternalKeyIndexEntry<T> entry, Func<object, bool>? filter, HashSet<long> emitted)
    {
        object element = _sequence.GetByOffset(entry.Offset);
        if (!_sequence.IsOriginalAndNotEmpty(element, entry.Offset)) return null;
        if (filter != null && !filter(element)) return null;
        return emitted.Add(entry.Offset) ? element : null;
    }

    private void PublishSnapshot(ExternalKeyIndexSnapshot<T> snapshot, long compactedThroughRevision)
    {
        _snapshot = snapshot;
        _dynamic.RemoveAll(entry => entry.Revision <= compactedThroughRevision);
    }

    private void MarkRebuildRequired()
    {
        if (_rebuildMarkerPath == null) return;
        File.WriteAllText(_rebuildMarkerPath, "rebuild-index-from-primary\n");
        _needsRebuild = true;
    }

    private bool HasKey(object element, T key)
    {
        foreach (T candidate in GetKeys(element))
        {
            if (KeyEquals(candidate, key)) return true;
        }

        return false;
    }

    private bool KeyEquals(T left, T right) => _comparer.Compare(left, right) == 0;

    private IEnumerable<T> GetKeys(object element) => _keysFunc(element) ?? Enumerable.Empty<T>();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing || _disposed) return;
        _keys.Dispose();
        _offsets.Dispose();
        _disposed = true;
    }
}
