using Polar.DB.Bench.Core.Abstractions;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record ArtifactDescriptor(
    [property: JsonPropertyName("role")] ArtifactRole Role,
    [property: JsonPropertyName("path")] string RelativePath,
    [property: JsonPropertyName("bytes")] long Bytes,
    [property: JsonPropertyName("notes")] string? Notes = null);
