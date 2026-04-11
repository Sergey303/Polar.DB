using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines the most valuable consistency tests for the repository-specific build and finalization workflow.
/// </summary>
/// <remarks>
/// The goal of these tests is to prove that <c>Build()</c> is not merely callable, but that it stabilizes durable
/// state in a way that remains logically consistent across repeated execution and durable reopen cycles.
/// </remarks>
public abstract class BuildConsistencyContractTests
{
    /// <summary>
    /// Creates a repository-specific harness bound to a concrete indexed sequence implementation.
    /// </summary>
    protected abstract IIndexedSequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that running build twice on an already stable sequence does not change logical state.
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
    /// Verifies that build produces a durable state whose data and lookup results survive reopen and refresh.
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
        Assert.Equal(new[] { 1, 2 }, harness.FindAllIndexesByKey("beta").ToArray());
        Assert.True(snapshot.StreamLength >= snapshot.AppendOffset);
    }

    /// <summary>
    /// Verifies that build after new appends keeps existing data readable and makes newly appended keyed data searchable.
    /// </summary>
    [Fact]
    public void Build_After_Append_Updates_Durable_State_Without_Losing_Previous_Items()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("base", "v-1"));
        harness.Append(harness.CreateIndexedValue("base", "v-2"));
        harness.Flush();
        harness.Build();

        harness.Append(harness.CreateIndexedValue("base", "v-3"));
        harness.Append(harness.CreateIndexedValue("tail", "v-4"));
        harness.Flush();
        harness.Build();
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(4, snapshot.Count);
        Assert.Equal(new[] { "v-1", "v-2", "v-3", "v-4" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, harness.FindAllIndexesByKey("base").ToArray());
        Assert.Equal(new[] { 3 }, harness.FindAllIndexesByKey("tail").ToArray());
    }
}
