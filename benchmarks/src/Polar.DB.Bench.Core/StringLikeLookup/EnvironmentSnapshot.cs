using System;
using System.Runtime.InteropServices;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record EnvironmentSnapshot(
    string MachineName,
    string OsDescription,
    string FrameworkDescription,
    string ProcessArchitecture,
    int ProcessorCount,
    bool Is64BitProcess,
    string? GitCommit,
    string? GitBranch)
{
    public static EnvironmentSnapshot Capture(string? gitCommit = null, string? gitBranch = null) =>
        new(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            Environment.Is64BitProcess,
            gitCommit,
            gitBranch);
}
