using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record EnvironmentManifest
{
    [JsonPropertyName("env")]
    public required string EnvironmentClass { get; init; }

    [JsonPropertyName("machine")]
    public required string MachineName { get; init; }

    [JsonPropertyName("os")]
    public required string OsDescription { get; init; }

    [JsonPropertyName("osArch")]
    public required string OsArchitecture { get; init; }

    [JsonPropertyName("processArch")]
    public required string ProcessArchitecture { get; init; }

    [JsonPropertyName("framework")]
    public required string FrameworkDescription { get; init; }

    [JsonPropertyName("is64Bit")]
    public required bool Is64BitProcess { get; init; }

    [JsonPropertyName("cpuCount")]
    public int ProcessorCount { get; init; }

    [JsonPropertyName("cwd")]
    public string? CurrentDirectory { get; init; }

    [JsonPropertyName("user")]
    public string? UserName { get; init; }

    [JsonPropertyName("git")]
    public GitManifest? Git { get; init; }
}
