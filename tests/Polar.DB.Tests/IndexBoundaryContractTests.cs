using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines the most valuable boundary tests for duplicate-key and missing-key index lookup.
/// </summary>
/// <remarks>
/// These tests intentionally target the places where binary-search based index code most often fails:
/// equal-range boundaries, absent keys near range edges, and stability after reopen/rebuild.
/// </remarks>
public abstract class IndexBoundaryContractTests
{
    /// <summary>
    /// Creates a repository-specific harness bound to a concrete indexed sequence implementation.
    /// </summary>
    protected abstract IIndexedSequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that lookup returns an empty range when the requested key is smaller than the minimum indexed key.
    /// </summary>
    [Fact]
    public void Lookup_Key_Before_Minimum_Returns_Empty_Result()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("b", "b-1"));
        harness.Append(harness.CreateIndexedValue("c", "c-1"));
        harness.Build();

        var result = harness.FindAllIndexesByKey("a");
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that lookup returns an empty range when the requested key is greater than the maximum indexed key.
    /// </summary>
    [Fact]
    public void Lookup_Key_After_Maximum_Returns_Empty_Result()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "a-1"));
        harness.Append(harness.CreateIndexedValue("b", "b-1"));
        harness.Build();

        var result = harness.FindAllIndexesByKey("z");
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that lookup returns an empty range when the requested key falls strictly between two indexed ranges.
    /// </summary>
    [Fact]
    public void Lookup_Missing_Key_Between_Ranges_Returns_Empty_Result()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "a-1"));
        harness.Append(harness.CreateIndexedValue("c", "c-1"));
        harness.Append(harness.CreateIndexedValue("e", "e-1"));
        harness.Build();

        var result = harness.FindAllIndexesByKey("b");
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that lookup returns the full duplicate block when equal keys begin at logical index zero.
    /// </summary>
    [Fact]
    public void Lookup_Returns_All_Duplicates_When_Equal_Range_Starts_At_Beginning()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("k", "k-1"));
        harness.Append(harness.CreateIndexedValue("k", "k-2"));
        harness.Append(harness.CreateIndexedValue("m", "m-1"));
        harness.Build();

        var result = harness.FindAllIndexesByKey("k");
        Assert.Equal(new[] { 0, 1 }, result.ToArray());
    }

    /// <summary>
    /// Verifies that lookup returns the full duplicate block when equal keys end at the last logical item.
    /// </summary>
    [Fact]
    public void Lookup_Returns_All_Duplicates_When_Equal_Range_Ends_At_Last_Item()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "a-1"));
        harness.Append(harness.CreateIndexedValue("k", "k-1"));
        harness.Append(harness.CreateIndexedValue("k", "k-2"));
        harness.Append(harness.CreateIndexedValue("k", "k-3"));
        harness.Build();

        var result = harness.FindAllIndexesByKey("k");
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    /// <summary>
    /// Verifies that lookup returns the entire logical range when all indexed items share the same key.
    /// </summary>
    [Fact]
    public void Lookup_When_All_Keys_Are_Equal_Returns_Entire_Range()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("same", "v-1"));
        harness.Append(harness.CreateIndexedValue("same", "v-2"));
        harness.Append(harness.CreateIndexedValue("same", "v-3"));
        harness.Append(harness.CreateIndexedValue("same", "v-4"));
        harness.Build();

        var result = harness.FindAllIndexesByKey("same");
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.ToArray());
    }

    /// <summary>
    /// Verifies that duplicate-key lookup remains correct after a durable reopen and rebuild cycle.
    /// </summary>
    [Fact]
    public void Lookup_Duplicate_Range_Remains_Correct_After_Reopen_And_Rebuild()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateIndexedValue("a", "a-1"));
        harness.Append(harness.CreateIndexedValue("d", "d-1"));
        harness.Append(harness.CreateIndexedValue("d", "d-2"));
        harness.Append(harness.CreateIndexedValue("d", "d-3"));
        harness.Append(harness.CreateIndexedValue("z", "z-1"));
        harness.Flush();
        harness.Build();
        harness.Reopen();
        harness.Refresh();
        harness.Build();

        var result = harness.FindAllIndexesByKey("d");
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }
}
