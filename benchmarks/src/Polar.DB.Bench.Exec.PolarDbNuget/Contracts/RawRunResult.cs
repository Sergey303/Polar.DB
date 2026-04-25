namespace Polar.DB.Bench.Exec.PolarDbNuget.Contracts;

internal sealed class RawRunResult
{
    public string RunId { get; set; } = "";
    public string EngineKey { get; set; } = "";
    public string Mode { get; set; } = "";
    public bool Success { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }
    public string? PolarDllPath { get; set; }
    public string? PolarAssemblyFullName { get; set; }
    public string? ExperimentId { get; set; }
    public string? ScenarioKey { get; set; }
    public Dictionary<string, object?> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ArtifactInfo> Artifacts { get; set; } = new();
    public ProbeReport? Probe { get; set; }
    public ErrorInfo? Error { get; set; }

    public static RawRunResult Failed(
        string runId,
        string engineKey,
        string mode,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        ErrorInfo error)
    {
        return new RawRunResult
        {
            RunId = runId,
            EngineKey = engineKey,
            Mode = mode,
            Success = false,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            Environment = EnvironmentInfo.Collect(),
            Error = error
        };
    }
}

internal sealed class ArtifactInfo
{
    public string Role { get; set; } = "unknown";
    public string Path { get; set; } = "";
    public long Bytes { get; set; }
}

internal sealed class ErrorInfo
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string? StackTrace { get; set; }

    public static ErrorInfo FromException(Exception ex)
    {
        return new ErrorInfo
        {
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            StackTrace = ex.ToString()
        };
    }
}

internal static class EnvironmentInfo
{
    public static Dictionary<string, object?> Collect()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["machineName"] = Environment.MachineName,
            ["osVersion"] = Environment.OSVersion.ToString(),
            ["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            ["osArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            ["frameworkDescription"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["processorCount"] = Environment.ProcessorCount,
            ["is64BitProcess"] = Environment.Is64BitProcess,
            ["currentDirectory"] = Environment.CurrentDirectory
        };
    }
}
