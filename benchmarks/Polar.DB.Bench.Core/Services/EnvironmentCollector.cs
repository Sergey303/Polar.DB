using System.Runtime.InteropServices;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.Services;

public static class EnvironmentCollector
{
    public static EnvironmentManifest Collect(string environmentClass, string? repositoryRoot = null)
    {
        return new EnvironmentManifest
        {
            EnvironmentClass = environmentClass,
            MachineName = Environment.MachineName,
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            Is64BitProcess = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount,
            CurrentDirectory = Environment.CurrentDirectory,
            UserName = Environment.UserName,
            Git = GitInfoReader.TryRead(repositoryRoot)
        };
    }
}
