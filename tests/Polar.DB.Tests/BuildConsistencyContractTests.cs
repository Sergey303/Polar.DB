using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines repository-level contract tests for build/finalization behavior of an indexed sequence.
/// </summary>
/// <remarks>
/// The core expectation is that <c>Build()</c> stabilizes data before index and state become durable, remains
/// idempotent on already stable data, and preserves searchability after reopen and refresh.
/// </remarks>
public abstract class BuildConsistencyContractTests
{
    /// <summary>
    /// Creates a concrete indexed sequence harness for the repository implementation under test.
    /// </summary>
    /// <returns>A fresh harness instance with isolated backing storage.</returns>
    protected abstract IIndexedSequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that running build twice on an unchanged sequence leaves logical data and index lookups unchanged.
    /// </summary>
    [Fact]
    public void Build_On_Stable_State_Is_Idempotent()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "one"));
        harness.Append(harness.CreateIndexedValue("b", "two"));
        harness.Append(harness.CreateIndexedValue("c", "three"));
        harness.Flush();
        harness.Build();

        var first = harness.Snapshot();
        var firstLookup = harness.FindAllIndexesByKey("b").ToArray();

        harness.Build();

        var second = harness.Snapshot();
        var secondLookup = harness.FindAllIndexesByKey("b").ToArray();

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.AppendOffset, second.AppendOffset);
        Assert.Equal(first.Items.Select(harness.ReadPayload), second.Items.Select(harness.ReadPayload));
        Assert.Equal(firstLookup, secondLookup);
    }

    /// <summary>
    /// Verifies that build produces durable data, durable state, and durable index search results after reopen.
    /// </summary>
    [Fact]
    public void Build_Produces_Durable_Data_State_And_Searchability()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("alpha", "a-1"));
        harness.Append(harness.CreateIndexedValue("beta", "b-1"));
        harness.Append(harness.CreateIndexedValue("beta", "b-2"));
        harness.Append(harness.CreateIndexedValue("gamma", "g-1"));
        harness.Flush();
        harness.Build();
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(4, snapshot.Count);
        Assert.Equal(new[] { "a-1", "b-1", "b-2", "g-1" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 1L, 2 }, harness.FindAllIndexesByKey("beta").ToArray());
        Assert.True(snapshot.StreamLength >= snapshot.AppendOffset);
    }
}
