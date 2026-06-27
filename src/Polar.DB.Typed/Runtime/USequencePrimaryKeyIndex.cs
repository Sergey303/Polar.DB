using Polar.Universal;

namespace Polar.DB.Typed.Runtime;

internal sealed class USequencePrimaryKeyIndex<TRecord> : IPrimaryKeyIndex<TRecord>
{
    private readonly USequence _sequence;
    private readonly Func<object, TRecord> _fromStorageRecord;

    public USequencePrimaryKeyIndex(
        USequence sequence,
        Func<object, TRecord> fromStorageRecord)
    {
        _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _fromStorageRecord = fromStorageRecord ?? throw new ArgumentNullException(nameof(fromStorageRecord));
    }

    public int Count => checked((int)_sequence.Count());

    public void Build()
    {
        _sequence.Build();
    }

    public bool Contains(IComparable key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return TryGet(key, out _);
    }

    public bool TryGet(IComparable key, out TRecord record)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        object? storageRecord = _sequence.GetByKey(key);
        if (storageRecord == null)
        {
            record = default!;
            return false;
        }

        record = _fromStorageRecord(storageRecord);
        return true;
    }
}
