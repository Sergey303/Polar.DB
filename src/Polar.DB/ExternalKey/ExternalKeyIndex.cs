using Polar.Universal;

namespace Polar.DB.ExternalKey;

public sealed class ExternalKeyIndex<T> : IUIndex, IExternalKeyIndex
    where T : IComparable<T>
{
    private readonly USequence _sequence;
    private readonly Func<object, IEnumerable<T>> _keysFunc;
    private readonly IComparer<T> _comparer;
    private readonly UniversalSequenceBase _keys;
    private readonly UniversalSequenceBase _offsets;
    private readonly List<ExternalKeyIndexEntry<T>> _dynamic = new();
    private T[] _keysArray = Array.Empty<T>();

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
        _keys = new UniversalSequenceBase(ExternalKeyIndexKeyCodec<T>.GetStorageType(), streamGen());
        _offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
    }

    public void Clear()
    {
        _keys.Clear();
        _offsets.Clear();
        _dynamic.Clear();
        _keysArray = Array.Empty<T>();
    }

    public void Flush()
    {
        _keys.Flush();
        _offsets.Flush();
    }

    public void Close()
    {
        _keys.Close();
        _offsets.Close();
    }

    public void Refresh()
    {
        _keys.Refresh();
        _offsets.Refresh();
        _dynamic.Clear();
        _keysArray = _keys.ElementValues().Select(ExternalKeyIndexKeyCodec<T>.FromStorage).ToArray();
    }

    public void Build()
    {
        var keys = new List<T>();
        var offsets = new List<long>();
        _sequence.Scan((off, obj) =>
        {
            foreach (var key in GetKeys(obj))
            {
                keys.Add(key);
                offsets.Add(off);
            }
            return true;
        });

        _keysArray = keys.ToArray();
        long[] offsetsArray = offsets.ToArray();
        Array.Sort(_keysArray, offsetsArray, _comparer);
        RewriteStaticStorage(offsetsArray);
        _dynamic.Clear();
    }

    public void Compact()
    {
        Build();
    }

    public void OnAppendElement(object element, long offset)
    {
        IComparable primary = _sequence.keyFunc(element);
        _dynamic.RemoveAll(entry => Equals(entry.Primary, primary));
        _dynamic.AddRange(GetKeys(element).Select(key => new ExternalKeyIndexEntry<T>(primary, key, offset)));
    }

    internal IEnumerable<object> GetManyByValue(T key) => GetManyByValue(key, null);

    internal IEnumerable<object> GetManyByValue(T key, Func<object, bool>? elementFilter)
    {
        var emittedOffsets = new HashSet<long>();
        foreach (var entry in _dynamic.Where(entry => KeyEquals(entry.Key, key)))
        {
            object? value = TryReadDynamic(entry, elementFilter, emittedOffsets);
            if (value != null) yield return value;
        }

        int pos = ExternalKeyIndexSearch.FindFirstEqual(_keysArray, key, _comparer);
        for (int i = pos; i >= 0 && i < _keysArray.Length && KeyEquals(_keysArray[i], key); i++)
        {
            object? value = TryReadStatic(i, key, elementFilter, emittedOffsets);
            if (value != null) yield return value;
        }
    }

    IEnumerable<object> IExternalKeyIndex.GetManyByValue(IComparable value) =>
        GetManyByValue(ExternalKeyIndexKeyCodec<T>.Cast(value));

    void IExternalKeyIndex.Compact()
    {
        Compact();
    }

    private object? TryReadStatic(int index, T key, Func<object, bool>? filter, HashSet<long> emitted)
    {
        long offset = (long)_offsets.GetByIndex(index);
        object element = _sequence.GetByOffset(offset);
        IComparable primary = _sequence.keyFunc(element);
        if (_sequence.ElementChanged(primary)) return null;
        if (_sequence.isEmpty(element)) return null;
        if (filter != null && !filter(element)) return null;
        if (filter == null && !HasKey(element, key)) return null;
        return emitted.Add(offset) ? element : null;
    }

    private object? TryReadDynamic(ExternalKeyIndexEntry<T> entry, Func<object, bool>? filter, HashSet<long> emitted)
    {
        if (!emitted.Add(entry.Offset)) return null;
        object element = _sequence.GetByOffset(entry.Offset);
        if (_sequence.isEmpty(element)) return null;
        if (filter != null && !filter(element)) return null;
        return element;
    }

    private bool HasKey(object element, T key) => GetKeys(element).Any(candidate => KeyEquals(candidate, key));

    private bool KeyEquals(T left, T right) => _comparer.Compare(left, right) == 0;

    private IEnumerable<T> GetKeys(object element) => _keysFunc(element) ?? Enumerable.Empty<T>();

    private void RewriteStaticStorage(long[] offsetsArray)
    {
        _keys.Clear();
        foreach (var key in _keysArray) _keys.AppendElement(ExternalKeyIndexKeyCodec<T>.ToStorage(key));
        _keys.Flush();
        _offsets.Clear();
        foreach (var offset in offsetsArray) _offsets.AppendElement(offset);
        _offsets.Flush();
    }
}
