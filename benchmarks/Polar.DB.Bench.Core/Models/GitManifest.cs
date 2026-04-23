using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record GitManifest
{
    [JsonPropertyName("commit")]
    public string? Commit { get; init; }

    [JsonPropertyName("branch")]
    public string? Branch { get; init; }
}
