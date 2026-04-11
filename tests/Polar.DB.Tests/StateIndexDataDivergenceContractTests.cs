using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines the most valuable divergence tests for durable data, state, and index artifacts.
/// </summary>
/// <remarks>
/// These tests target the class of bugs that rarely appear in simple unit tests: situations where one durable artifact
/// reflects an older repository state than another. The repository should either repair such drift or fail loudly.
///
/// The contract pack intentionally avoids dynamic runtime skip APIs because not every consumer uses xUnit v3 or
/// additional skip helpers. When a concrete harness reports that a given durable artifact does not exist in that
/// repository implementation, the corresponding test exits early and is treated as not applicable.
/// </remarks>
public abstract class StateIndexDataDivergenceContractTests
{
    /// <summary>
    /// Creates a repository-specific harness bound to a concrete indexed sequence implementation.
    /// </summary>
    protected abstract IIndexedSequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that refresh or recovery does not trust a stale state artifact more than readable durable data.
    /// </summary>
    [Fact]
    public void Refresh_Prefers_Readable_Data_Over_Stale_State_Artifact()
    {
        using var harness = CreateHarness();
        if (!harness.HasStateArtifacts)
        {
            return;
        }

        harness.Append(harness.CreateIndexedValue("k", "v-1"));
        harness.Append(harness.CreateIndexedValue("k", "v-2"));
        harness.Flush();
        harness.Build();
        harness.SnapshotArtifacts("stable-two");

        harness.Append(harness.CreateIndexedValue("k", "v-3"));
        harness.Flush();
        harness.Build();

        harness.RestoreArtifacts("stable-two", ArtifactKinds.State);
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(new[] { "v-1", "v-2", "v-3" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(snapshot.StreamLength >= snapshot.AppendOffset);
    }

    /// <summary>
    /// Verifies that build repairs or rebuilds index artifacts when they lag behind current durable data.
    /// </summary>
    [Fact]
    public void Build_Repairs_Stale_Index_Artifacts_After_Data_Has_Advanced()
    {
        using var harness = CreateHarness();
        if (!harness.HasIndexArtifacts)
        {
            return;
        }

        harness.Append(harness.CreateIndexedValue("dup", "v-1"));
        harness.Append(harness.CreateIndexedValue("dup", "v-2"));
        harness.Flush();
        harness.Build();
        harness.SnapshotArtifacts("before-third");

        harness.Append(harness.CreateIndexedValue("dup", "v-3"));
        harness.Flush();
        harness.Build();

        harness.RestoreArtifacts("before-third", ArtifactKinds.Index);
        harness.Reopen();
        harness.Refresh();
        harness.Build();

        var snapshot = harness.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(new[] { "v-1", "v-2", "v-3" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, harness.FindAllIndexesByKey("dup").ToArray());
    }

    /// <summary>
    /// Verifies that deleting state and index sidecars does not make durable data unreadable after repository repair steps.
    /// </summary>
    [Fact]
    public void Missing_State_And_Index_Artifacts_Do_Not_Destroy_Readable_Data()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "v-1"));
        harness.Append(harness.CreateIndexedValue("b", "v-2"));
        harness.Append(harness.CreateIndexedValue("b", "v-3"));
        harness.Flush();
        harness.Build();

        var deleteKinds = ArtifactKinds.None;
        if (harness.HasStateArtifacts)
        {
            deleteKinds |= ArtifactKinds.State;
        }

        if (harness.HasIndexArtifacts)
        {
            deleteKinds |= ArtifactKinds.Index;
        }

        if (deleteKinds == ArtifactKinds.None)
        {
            return;
        }

        harness.DeleteArtifacts(deleteKinds);
        harness.Reopen();
        harness.Refresh();
        harness.Build();

        var snapshot = harness.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(new[] { "v-1", "v-2", "v-3" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.Equal(new[] { 1, 2 }, harness.FindAllIndexesByKey("b").ToArray());
    }
}
