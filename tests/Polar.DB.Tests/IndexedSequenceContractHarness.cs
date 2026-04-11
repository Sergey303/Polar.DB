namespace Polar.DB.Tests;

/// <summary>
/// Extends <see cref="ISequenceContractHarness"/> with the additional capabilities needed for index and artifact-consistency tests.
/// </summary>
/// <remarks>
/// <para>
/// The top-3 storage tests focus on lifecycle, recovery, and variable-size rewrite. The next tier of high-value tests
/// focuses on the places where durable artifacts can silently diverge: data, state, and indexes.
/// </para>
/// <para>
/// This abstraction keeps those tests reusable across repository-specific sequence fixtures. A concrete implementation
/// can map these operations to whatever file layout and index API the repository currently uses.
/// </para>
/// </remarks>
public interface IIndexedSequenceContractHarness : ISequenceContractHarness
{
    /// <summary>
    /// Gets a value indicating whether the implementation produces durable index artifacts that can become stale.
    /// </summary>
    bool HasIndexArtifacts { get; }

    /// <summary>
    /// Gets a value indicating whether the implementation produces durable state artifacts that can become stale.
    /// </summary>
    bool HasStateArtifacts { get; }

    /// <summary>
    /// Creates a repository-specific value with a searchable key and readable payload.
    /// </summary>
    /// <param name="key">Logical key used by index lookup tests.</param>
    /// <param name="payload">Logical payload used by readability assertions.</param>
    /// <returns>A value object suitable for append operations.</returns>
    object CreateIndexedValue(string key, string payload);

    /// <summary>
    /// Reads the logical key from a stored item.
    /// </summary>
    /// <param name="value">Repository-specific value returned by the sequence.</param>
    /// <returns>The key associated with the item.</returns>
    string ReadKey(object value);

    /// <summary>
    /// Performs repository-specific index lookup and returns all matching logical item indexes in ascending order.
    /// </summary>
    /// <param name="key">Lookup key.</param>
    /// <returns>Logical indexes of all matching items.</returns>
    IReadOnlyList<int> FindAllIndexesByKey(string key);

    /// <summary>
    /// Saves the current durable artifacts under a repository-specific snapshot name.
    /// </summary>
    /// <param name="snapshotName">Stable name of the snapshot that may later be restored partially.</param>
    void SnapshotArtifacts(string snapshotName);

    /// <summary>
    /// Restores a subset of previously snapshotted artifacts while leaving all other durable artifacts unchanged.
    /// </summary>
    /// <param name="snapshotName">Name of the snapshot created earlier by <see cref="SnapshotArtifacts"/>.</param>
    /// <param name="kinds">Artifact kinds that should be restored from the snapshot.</param>
    void RestoreArtifacts(string snapshotName, ArtifactKinds kinds);

    /// <summary>
    /// Deletes a subset of durable artifacts to simulate missing sidecar metadata.
    /// </summary>
    /// <param name="kinds">Artifact kinds that should be deleted.</param>
    void DeleteArtifacts(ArtifactKinds kinds);
}

/// <summary>
/// Identifies which durable repository artifacts should be manipulated by artifact-divergence tests.
/// </summary>
[Flags]
public enum ArtifactKinds
{
    /// <summary>
    /// No artifacts.
    /// </summary>
    None = 0,

    /// <summary>
    /// The primary data file.
    /// </summary>
    Data = 1,

    /// <summary>
    /// Durable state or sidecar state metadata.
    /// </summary>
    State = 2,

    /// <summary>
    /// Durable index artifacts.
    /// </summary>
    Index = 4,

    /// <summary>
    /// All durable artifacts.
    /// </summary>
    All = Data | State | Index,
}
