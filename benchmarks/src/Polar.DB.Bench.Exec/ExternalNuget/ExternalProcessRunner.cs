using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Polar.DB.Bench.Exec.ExternalNuget;

internal static class ExternalProcessRunner
{
    public static ExternalProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
                Console.Error.WriteLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {fileName}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        process.WaitForExit();

        return new ExternalProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
