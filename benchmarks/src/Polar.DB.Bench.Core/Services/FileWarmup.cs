using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.Services;

/// <summary>
/// Simple physical file warmup helper.
/// Reads all files under a directory sequentially to prime the OS file cache
/// before measured lookup/search operations.
/// </summary>
public static class FileWarmup
{
    /// <summary>
    /// Default buffer size for warmup reads: 1 MiB.
    /// </summary>
    public const int DefaultBufferSize = 1 * 1024 * 1024;

    /// <summary>
    /// Reads all files under <paramref name="directoryPath"/> sequentially
    /// using <see cref="FileOptions.SequentialScan"/> to warm the OS page cache.
    /// Files that disappear or become locked between enumeration and open are silently skipped.
    /// </summary>
    /// <param name="directoryPath">The directory to scan for files.</param>
    /// <param name="bufferSize">Read buffer size in bytes. Defaults to 1 MiB.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static void WarmDirectory(
        string directoryPath,
        int bufferSize = DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        var buffer = new byte[bufferSize];

        foreach (var filePath in Directory.EnumerateFiles(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize,
                    FileOptions.SequentialScan);

                // Read the entire file sequentially
                while (stream.Read(buffer, 0, buffer.Length) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (DirectoryNotFoundException)
            {
                // File or directory was deleted between enumeration and open; skip.
            }
            catch (FileNotFoundException)
            {
                // File was deleted between enumeration and open; skip.
            }
            catch (IOException)
            {
                // File is locked or became inaccessible; skip.
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to read; skip.
            }
        }
    }

    /// <summary>
    /// Determines whether file warmup is enabled based on workload options.
    /// Only an explicit case-insensitive string "false" disables warmup.
    /// Any other value (absent, empty, "true", "file", etc.) means warmup is enabled.
    /// </summary>
    /// <param name="parameters">Workload parameters dictionary (options).</param>
    /// <returns>true if warmup should be performed; false only if warm is explicitly "false".</returns>
    public static bool IsWarmEnabled(IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return true;
        }

        // Try exact match first
        if (parameters.TryGetValue("warm", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return !string.Equals(value.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        }

        // Fallback: case-insensitive scan
        foreach (var pair in parameters)
        {
            if (!string.Equals(pair.Key, "warm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                return true;
            }

            return !string.Equals(pair.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}
