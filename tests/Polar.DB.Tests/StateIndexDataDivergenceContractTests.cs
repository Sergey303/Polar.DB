using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Describes the minimal adapter required to run index/data/state consistency contract tests against a concrete sequence.
/// </summary>
/// <remarks>
/// Implement this interface in the repository test project when the concrete indexed sequence type is known. The
/// contract tests can then verify real <c>Build()</c>, reopen, refresh, corruption, and lookup semantics without being
/// coupled to one particular constructor shape.
/// </remarks>
public interface IIndexedSequenceContractHarness : IDisposable
{
    /// <summary>
    /// Appends a repository-specific indexed value to the underlying sequence.
    /// </summary>
    /// <param name="value">The value created by <see cref="CreateIndexedValue"/>.</param>
    void Append(object value);

    /// <summary>
    /// Creates a value whose key can be found through the index and whose payload can be validated after reads.
    /// </summary>
    /// <param name="key">The logical key used by index lookup.</param>
    /// <param name="payload">The payload used to verify data order and durability.</param>
    /// <returns>A repository-specific value suitable for appending to the indexed sequence.</returns>
    object CreateIndexedValue(string key, string payload);

    /// <summary>
    /// Extracts the payload portion from a repository-specific indexed value.
    /// </summary>
    /// <param name="value">The value returned by a sequence read or snapshot.</param>
    /// <returns>The string payload stored in <paramref name="value"/>.</returns>
    string ReadPayload(object value);

    /// <summary>
    /// Flushes sequence data to its backing storage.
    /// </summary>
    void Flush();

    /// <summary>
    /// Builds or rebuilds the repository-specific index and finalizes durable sequence state.
    /// </summary>
    void Build();

    /// <summary>
    /// Reopens the underlying sequence, state, and index resources to simulate a process restart.
    /// </summary>
    void Reopen();

    /// <summary>
    /// Refreshes the reopened sequence state from persistent storage.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Corrupts the persisted state count to simulate stale or overdeclared state.
    /// </summary>
    /// <param name="count">The count value to force into the persistent state representation.</param>
    void CorruptStateCount(long count);

    /// <summary>
    /// Appends arbitrary bytes to the data file behind the indexed sequence.
    /// </summary>
    /// <param name="bytes">The bytes to append as garbage or stale physical tail.</param>
    void AppendGarbageToDataFile(byte[] bytes);

    /// <summary>
    /// Finds all logical item indexes matching a key through the repository-specific index implementation.
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <returns>The logical zero-based item indexes returned by the index.</returns>
    IReadOnlyList<int> FindAllIndexesByKey(string key);

    /// <summary>
    /// Captures current logical sequence state, physical stream length, and readable items.
    /// </summary>
    /// <returns>A snapshot of the current sequence state.</returns>
    IndexedSequenceSnapshot Snapshot();
}

/// <summary>
/// Represents an immutable snapshot of sequence state used by index/data/state contract tests.
/// </summary>
/// <param name="Count">The logical element count reported by the sequence.</param>
/// <param name="AppendOffset">The logical append boundary where the next item should be written.</param>
/// <param name="StreamLength">The physical length of the data stream or data file.</param>
/// <param name="Items">The logical items readable from the sequence in order.</param>
public sealed record IndexedSequenceSnapshot(long Count, long AppendOffset, long StreamLength, IReadOnlyList<object> Items);

/// <summary>
/// Defines contract tests for divergence between data file, state file, and index file.
/// </summary>
/// <remarks>
/// The tests focus on the most dangerous storage failures: index built over stale data, state count pointing beyond
/// valid data, and rebuilds that lose old items while adding new ones.
/// </remarks>
public abstract class StateIndexDataDivergenceContractTests
{
    /// <summary>
    /// Creates a concrete harness for the repository-specific indexed sequence implementation.
    /// </summary>
    /// <returns>A fresh harness instance with isolated backing files or streams.</returns>
    protected abstract IIndexedSequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that data, state, and index stay mutually consistent after build, reopen, and refresh.
    /// </summary>
    [Fact]
    public void Data_State_Index_Remain_Consistent_After_Build_Reopen_Refresh()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "one"));
        harness.Append(harness.CreateIndexedValue("b", "two"));
        harness.Append(harness.CreateIndexedValue("b", "three"));
        harness.Flush();
        harness.Build();
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(3L, snapshot.Count);
        Assert.True(snapshot.StreamLength >= snapshot.AppendOffset);
        Assert.Equal(new[] { "one", "two", "three" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 1, 2 }, harness.FindAllIndexesByKey("b").ToArray());
    }

    /// <summary>
    /// Verifies that a stale state count and garbage data tail do not make invalid bytes searchable through the index.
    /// </summary>
    [Fact]
    public void Stale_State_Count_Does_Not_Make_Garbage_Data_Searchable()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "one"));
        harness.Append(harness.CreateIndexedValue("b", "two"));
        harness.Flush();
        harness.Build();

        harness.CorruptStateCount(3L);
        harness.AppendGarbageToDataFile(new byte[] { 0xAA, 0xBB, 0xCC });
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(2L, snapshot.Count);
        Assert.Equal(new[] { "one", "two" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 1 }, harness.FindAllIndexesByKey("b").ToArray());
    }

    /// <summary>
    /// Verifies that rebuilding after additional appends keeps old data and makes newly appended keys searchable.
    /// </summary>
    [Fact]
    public void Rebuild_After_New_Appends_Updates_Index_Without_Losing_Old_Data()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("base", "v1"));
        harness.Append(harness.CreateIndexedValue("base", "v2"));
        harness.Flush();
        harness.Build();

        harness.Append(harness.CreateIndexedValue("base", "v3"));
        harness.Append(harness.CreateIndexedValue("tail", "v4"));
        harness.Flush();
        harness.Build();
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(new[] { "v1", "v2", "v3", "v4" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, harness.FindAllIndexesByKey("base").ToArray());
        Assert.Equal(new[] { 3 }, harness.FindAllIndexesByKey("tail").ToArray());
    }
}
