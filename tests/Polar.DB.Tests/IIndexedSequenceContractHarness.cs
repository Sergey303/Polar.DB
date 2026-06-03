namespace Polar.DB.Tests;

/// <summary>
/// Repository-specific harness used by indexed-sequence contract tests.
/// </summary>
public interface IIndexedSequenceContractHarness : IDisposable
{
    object CreateIndexedValue(string key, string payload);

    void Append(object value);

    void Flush();

    void Build();

    void Reopen();

    void Refresh();

    IndexedSequenceSnapshot Snapshot();

    IReadOnlyList<long> FindAllIndexesByKey(string key);

    string ReadPayload(object value);

    void CorruptDeclaredCount(long declaredCount);

    void AppendGarbageTail(byte[] bytes);
}