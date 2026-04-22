using Polar.DB.Bench.Core.Abstractions;

namespace Polar.DB.Bench.Core.Models;

public sealed record ArtifactDescriptor(
    ArtifactRole Role,
    string RelativePath,
    long Bytes,
    string? Notes = null);
