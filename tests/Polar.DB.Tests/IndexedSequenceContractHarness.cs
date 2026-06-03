namespace Polar.DB.Tests;



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
