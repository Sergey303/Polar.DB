using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines the highest-value lifecycle tests for sequence storage.
/// </summary>
/// <remarks>
/// These tests intentionally span multiple operations and process lifecycles because the most dangerous bugs in a
/// storage library rarely live in a single method. They live in the gaps between append, flush, reopen, refresh,
/// and append-again.
/// </remarks>
public abstract class SequenceLifecycleContractTests
{
    /// <summary>
    /// Creates a repository-specific harness bound to a concrete sequence implementation.
    /// </summary>
    protected abstract ISequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that a persisted sequence survives a full reopen/refresh cycle and remains appendable afterwards.
    /// </summary>
    /// <remarks>
    /// This is the most important lifecycle scenario for the repository because it checks that logical state is
    /// reconstructed from durable data rather than from incidental in-memory cursor state.
    /// </remarks>
    [Fact]
    public void Append_Reopen_Refresh_AppendAgain_Preserves_Data_Count_And_Logical_End()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateValue("alpha"));
        harness.Append(harness.CreateValue("beta"));
        harness.Flush();
        harness.Build();

        var beforeReopen = harness.Snapshot();
        Assert.Equal(2, beforeReopen.Count);
        Assert.Equal(new[] { "alpha", "beta" }, beforeReopen.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(beforeReopen.AppendOffset > 0);
        Assert.True(beforeReopen.StreamLength >= beforeReopen.AppendOffset);

        harness.Reopen();
        harness.Refresh();

        var afterRefresh = harness.Snapshot();
        Assert.Equal(2, afterRefresh.Count);
        Assert.Equal(new[] { "alpha", "beta" }, afterRefresh.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(afterRefresh.AppendOffset > 0);
        Assert.True(afterRefresh.StreamLength >= afterRefresh.AppendOffset);

        harness.Append(harness.CreateValue("gamma"));
        harness.Flush();
        harness.Build();

        var afterSecondAppend = harness.Snapshot();
        Assert.Equal(3, afterSecondAppend.Count);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, afterSecondAppend.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(afterSecondAppend.AppendOffset >= afterRefresh.AppendOffset);

        harness.Reopen();
        harness.Refresh();

        var finalSnapshot = harness.Snapshot();
        Assert.Equal(3, finalSnapshot.Count);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, finalSnapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(finalSnapshot.AppendOffset > 0);
        Assert.True(finalSnapshot.StreamLength >= finalSnapshot.AppendOffset);
    }

    /// <summary>
    /// Verifies that refresh is idempotent once the sequence has already been normalized.
    /// </summary>
    /// <remarks>
    /// Refresh that keeps changing logical state on every call is a classic source of restart-only corruption bugs.
    /// This test locks down the expectation that normalization converges.
    /// </remarks>
    [Fact]
    public void Refresh_After_Normalization_Is_Idempotent()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateValue("one"));
        harness.Append(harness.CreateValue("two"));
        harness.Append(harness.CreateValue("three"));
        harness.Flush();
        harness.Build();
        harness.Reopen();

        harness.Refresh();
        var first = harness.Snapshot();

        harness.Refresh();
        var second = harness.Snapshot();

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.AppendOffset, second.AppendOffset);
        Assert.Equal(first.Items.Select(harness.ReadPayload), second.Items.Select(harness.ReadPayload));
    }
}
