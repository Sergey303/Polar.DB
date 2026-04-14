using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines contract tests for indexed lookup behavior at duplicate-key and missing-key boundaries.
/// </summary>
/// <remarks>
/// These tests protect the binary-search boundary semantics that are easy to break when an index contains repeated
/// keys. A lookup must start from the first matching item in the equal range rather than an arbitrary duplicate.
/// </remarks>
public abstract class IndexBoundaryContractTests
{
    /// <summary>
    /// Creates a concrete indexed sequence harness for boundary lookup tests.
    /// </summary>
    /// <returns>A fresh harness instance with isolated backing storage.</returns>
    protected abstract IIndexedSequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that duplicate-key lookup returns the full equal range starting at the first matching position.
    /// </summary>
    [Fact]
    public void Duplicate_Key_Lookup_Returns_First_Equal_Range_Position()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("10", "first"));
        harness.Append(harness.CreateIndexedValue("10", "second"));
        harness.Append(harness.CreateIndexedValue("10", "third"));
        harness.Append(harness.CreateIndexedValue("20", "tail"));
        harness.Flush();
        harness.Build();

        Assert.Equal(new[] { 0, 1, 2 }, harness.FindAllIndexesByKey("10").ToArray());
    }

    /// <summary>
    /// Verifies that duplicate-key lookup also works when the equal range is located at the end of the index.
    /// </summary>
    [Fact]
    public void Duplicate_Key_Lookup_At_End_Boundary_Returns_Full_Block()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("10", "head"));
        harness.Append(harness.CreateIndexedValue("20", "first"));
        harness.Append(harness.CreateIndexedValue("20", "second"));
        harness.Flush();
        harness.Build();

        Assert.Equal(new[] { 1, 2 }, harness.FindAllIndexesByKey("20").ToArray());
    }

    /// <summary>
    /// Verifies that a missing-key lookup returns an empty range and does not mutate sequence state.
    /// </summary>
    [Fact]
    public void Missing_Key_Lookup_Returns_Empty_Range_Without_Touching_Data_State()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "one"));
        harness.Append(harness.CreateIndexedValue("c", "three"));
        harness.Flush();
        harness.Build();

        var before = harness.Snapshot();
        Assert.Empty(harness.FindAllIndexesByKey("b"));
        var after = harness.Snapshot();

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.AppendOffset, after.AppendOffset);
    }
}
