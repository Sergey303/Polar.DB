namespace Polar.DB.Bench.Core.Abstractions;

public enum ArtifactRole
{
    Unknown,
    PrimaryData,
    SecondaryIndex,
    State,
    PrimaryDatabase,
    Wal,
    SharedMemory,
    Metadata,
    Checkpoint,
    Journal,
    Temporary,
    Report
}
