using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines the highest-value rewrite tests for variable-size sequence items.
/// </summary>
/// <remarks>
/// These tests are intentionally strict because variable-size in-place rewrite is where storage libraries most often
/// drift into silent corruption. If the implementation allows such rewrites, it must preserve all logical invariants.
/// If it does not allow them, it should fail loudly and leave the file unchanged.
/// </remarks>
public abstract class VariableSizeRewriteContractTests
{
    /// <summary>
    /// Creates a repository-specific harness bound to a concrete variable-size sequence implementation.
    /// </summary>
    protected abstract ISequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that rewriting an item with another item of the same logical size preserves neighboring data and append state.
    /// </summary>
    [Fact]
    public void Rewrite_Same_Size_Value_Preserves_Neighboring_Items_And_Remains_Readable_After_Reopen()
    {
        using var harness = CreateHarness();
        Assert.True(harness.IsVariableSize);

        harness.Append(harness.CreateValue("aaaa"));
        harness.Append(harness.CreateValue("bbbb"));
        harness.Append(harness.CreateValue("cccc"));
        harness.Flush();
        harness.Build();

        var beforeRewrite = harness.Snapshot();

        harness.RewriteAt(1, harness.CreateValue("zzzz"));
        harness.Flush();
        harness.Build();
        harness.Reopen();
        harness.Refresh();

        var afterRewrite = harness.Snapshot();
        Assert.Equal(3, afterRewrite.Count);
        Assert.Equal(new[] { "aaaa", "zzzz", "cccc" }, afterRewrite.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(afterRewrite.AppendOffset <= afterRewrite.StreamLength);
        Assert.True(afterRewrite.AppendOffset >= beforeRewrite.AppendOffset || afterRewrite.AppendOffset > 0);
    }

    /// <summary>
    /// Verifies that rewriting an item with a shorter payload does not damage subsequent items.
    /// </summary>
    [Fact]
    public void Rewrite_Shorter_Value_Does_Not_Corrupt_Following_Items()
    {
        using var harness = CreateHarness();
        Assert.True(harness.IsVariableSize);

        harness.Append(harness.CreateValue("long-long-value"));
        harness.Append(harness.CreateValue("middle"));
        harness.Append(harness.CreateValue("tail"));
        harness.Flush();
        harness.Build();

        harness.RewriteAt(0, harness.CreateValue("x"));
        harness.Flush();
        harness.Build();
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(new[] { "x", "middle", "tail" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(snapshot.StreamLength >= snapshot.AppendOffset);
    }

    /// <summary>
    /// Verifies that rewriting an item with a longer payload does not silently corrupt the sequence.
    /// </summary>
    /// <remarks>
    /// The preferred contract for this repository is a loud failure with unchanged durable data. If the implementation
    /// intentionally supports relocation-based rewrite, the harness can adapt this test into a success-path variant.
    /// Until such support is explicit, silent success is treated as suspicious.
    /// </remarks>
    [Fact]
    public void Rewrite_Longer_Value_Fails_Loudly_Or_Preserves_Durable_State()
    {
        using var harness = CreateHarness();
        Assert.True(harness.IsVariableSize);

        harness.Append(harness.CreateValue("aa"));
        harness.Append(harness.CreateValue("bb"));
        harness.Append(harness.CreateValue("cc"));
        harness.Flush();
        harness.Build();

        var before = harness.Snapshot();

        var exception = Record.Exception(() =>
        {
            harness.RewriteAt(1, harness.CreateValue("this-value-is-longer-than-the-original"));
            harness.Flush();
            harness.Build();
        });

        harness.Reopen();

        if (exception is null)
        {
            harness.Refresh();
            var after = harness.Snapshot();
            Assert.Equal(3, after.Count);
            Assert.True(after.StreamLength >= after.AppendOffset);
            Assert.Equal("aa", harness.ReadPayload(after.Items[0]));
            Assert.Equal("cc", harness.ReadPayload(after.Items[2]));
        }
        else
        {
            harness.Refresh();
            var afterFailure = harness.Snapshot();
            Assert.Equal(before.Count, afterFailure.Count);
            Assert.Equal(before.Items.Select(harness.ReadPayload), afterFailure.Items.Select(harness.ReadPayload));
            Assert.True(afterFailure.StreamLength >= afterFailure.AppendOffset);
        }
    }
}
