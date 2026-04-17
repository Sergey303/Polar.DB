using Xunit;

namespace Polar.DB.Tests;



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
        Assert.Equal(new[] { 1L, 2 }, harness.FindAllIndexesByKey("b").ToArray());
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

        harness.CorruptDeclaredCount(3);
        harness.AppendGarbageTail(new byte[] { 0xAA, 0xBB, 0xCC });
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(2L, snapshot.Count);
        Assert.Equal(new[] { "one", "two" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 1L }, harness.FindAllIndexesByKey("b").ToArray());
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
        Assert.Equal(new[] { 0L, 1, 2 }, harness.FindAllIndexesByKey("base").ToArray());
        Assert.Equal(new[] { 3L }, harness.FindAllIndexesByKey("tail").ToArray());
    }
}
