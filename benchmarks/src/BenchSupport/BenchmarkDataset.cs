using System.Collections;

namespace PolarDbBenchmarks;

// Reconstructs rows deterministically when enumerated or indexed, without keeping the dataset in RAM.
internal sealed class BenchmarkDataset : IReadOnlyList<Row>
{
    private readonly ExperimentKind _kind;
    private readonly long _startId;

    public BenchmarkDataset(int count, ExperimentKind kind, long startId = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Count = count;
        _kind = kind;
        _startId = startId;
    }

    public int Count { get; }
    public int Length => Count;
    public long LongLength => Count;

    public Row this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return BenchmarkData.CreateRow(checked(_startId + index), _kind);
        }
    }

    public IEnumerator<Row> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
