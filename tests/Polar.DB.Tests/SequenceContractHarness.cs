namespace Polar.DB.Tests;

/// <summary>
/// Defines the minimum contract that high-value sequence storage tests need from a concrete test fixture.
/// </summary>
/// <remarks>
/// <para>
/// The goal of this abstraction is to keep the tests focused on storage invariants rather than on the exact
/// constructor shape of a particular <c>USequence</c>/<c>UniversalSequenceBase</c> implementation.
/// </para>
/// <para>
/// A repository-specific fixture should implement this interface for the concrete sequence type used in the test
/// project. Once such a fixture exists, the abstract contract test classes in this folder can be inherited as-is.
/// </para>
/// </remarks>
public interface ISequenceContractHarness : IDisposable
{
    /// <summary>
    /// Gets the path to the main data file used by the sequence under test.
    /// </summary>
    string DataFilePath { get; }

    /// <summary>
    /// Gets the path to the sidecar state file when the implementation uses one.
    /// </summary>
    string? StateFilePath { get; }

    /// <summary>
    /// Gets a value indicating whether the tested sequence stores variable-size items.
    /// </summary>
    bool IsVariableSize { get; }

    /// <summary>
    /// Creates a value that can be appended to the sequence.
    /// </summary>
    /// <param name="payload">The logical payload that should later be readable from the stored item.</param>
    /// <returns>A repository-specific value object suitable for append or rewrite operations.</returns>
    object CreateValue(string payload);

    /// <summary>
    /// Extracts the logical payload from a stored item.
    /// </summary>
    /// <param name="value">The repository-specific value returned by the sequence.</param>
    /// <returns>The logical payload used for readable assertions in the contract tests.</returns>
    string ReadPayload(object value);

    /// <summary>
    /// Appends a new item to the sequence.
    /// </summary>
    void Append(object value);

    /// <summary>
    /// Rewrites an existing item in place.
    /// </summary>
    /// <param name="index">Zero-based logical item index.</param>
    /// <param name="value">New value to store at the specified logical index.</param>
    void RewriteAt(int index, object value);

    /// <summary>
    /// Forces the sequence to flush or otherwise persist its current logical state.
    /// </summary>
    void Flush();

    /// <summary>
    /// Executes the repository-specific build/finalization workflow.
    /// </summary>
    void Build();

    /// <summary>
    /// Re-opens the sequence from disk so that subsequent operations observe persisted state rather than in-memory state.
    /// </summary>
    void Reopen();

    /// <summary>
    /// Runs the repository-specific refresh or recovery logic.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Returns a snapshot of the current logical state.
    /// </summary>
    SequenceSnapshot Snapshot();

    /// <summary>
    /// Corrupts only the declared element count in the sequence header while keeping the rest of the file untouched.
    /// </summary>
    /// <param name="declaredCount">The fake element count that should be written into the header.</param>
    void CorruptDeclaredCount(int declaredCount);

    /// <summary>
    /// Appends arbitrary trailing bytes to the main data file without updating sequence metadata.
    /// </summary>
    /// <param name="tail">Garbage bytes that should not become logical data.</param>
    void AppendGarbageTail(byte[] tail);

    /// <summary>
    /// Truncates the main data file to the specified byte length.
    /// </summary>
    /// <param name="length">New file length in bytes.</param>
    void TruncateDataFile(long length);

    /// <summary>
    /// Replaces the current file with a deliberately partial or invalid header-only payload.
    /// </summary>
    /// <param name="headerBytes">Raw bytes that should be treated as a damaged header.</param>
    void ReplaceWithRawHeader(byte[] headerBytes);
}

/// <summary>
/// Represents the sequence state that the contract tests verify across reopen, refresh, corruption, and rewrite scenarios.
/// </summary>
/// <param name="Count">Logical number of readable items.</param>
/// <param name="AppendOffset">Logical append position used by the implementation.</param>
/// <param name="StreamLength">Physical length of the underlying data stream.</param>
/// <param name="Items">Readable logical items in logical order.</param>
public sealed record SequenceSnapshot(
    int Count,
    long AppendOffset,
    long StreamLength,
    IReadOnlyList<object> Items);
