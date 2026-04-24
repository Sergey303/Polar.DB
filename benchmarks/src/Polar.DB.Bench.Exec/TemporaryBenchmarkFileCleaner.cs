using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Polar.DB.Bench.Exec;

public sealed class TemporaryBenchmarkFileCleaner
{
    private static readonly string[] DefaultDeletePatterns =
    [
        "*.db",
        "*.db-wal",
        "*.db-shm",

        "*.sqlite",
        "*.sqlite-wal",
        "*.sqlite-shm",

        "*.state",
        "*.index",

        // exact / common Polar.DB names
        "polar.db",
        "Polar.DB",
        "*.polar.db",
        "*.polardb",

        // optional Polar.DB benchmark artifacts if adapters name files this way
        "*.pdb",
        "*.pdbbin",
        "*.pdbstate"
    ];

    private readonly string[] _deletePatterns;
    private readonly int _maxAttempts;
    private readonly TimeSpan _retryDelay;

    public TemporaryBenchmarkFileCleaner(
        IEnumerable<string>? deletePatterns = null,
        int maxAttempts = 5,
        TimeSpan? retryDelay = null)
    {
        _deletePatterns = (deletePatterns ?? DefaultDeletePatterns)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _maxAttempts = Math.Max(1, maxAttempts);
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(150);
    }

    public CleanupReport CleanRunTemporaryFiles(RunPaths paths)
    {
        if (paths is null)
            throw new ArgumentNullException(nameof(paths));

        // Защитный принцип:
        // удаляем только внутри run-папки, а не по всему benchmarks/
        // и точно не трогаем results/raw.
        var allowedRoots = new[]
        {
            paths.WorkDirectory,
            paths.DataDirectory,
            paths.ArtifactsDirectory
        };

        var deleted = new List<string>();
        var failed = new List<CleanupFailure>();

        foreach (var root in allowedRoots)
        {
            if (!Directory.Exists(root))
                continue;

            EnsureDirectoryIsInside(paths.RunDirectory, root);

            foreach (var file in EnumerateFilesByPatterns(root, _deletePatterns))
            {
                TryDeleteFile(file, deleted, failed);
            }

            TryDeleteEmptyDirectories(root, failed);
        }

        return new CleanupReport(
            deleted.Count,
            failed.Count,
            deleted,
            failed);
    }

    private static IEnumerable<string> EnumerateFilesByPatterns(string root, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(
                    root,
                    pattern,
                    SearchOption.AllDirectories);
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
                yield return file;
        }
    }

    private void TryDeleteFile(
        string file,
        List<string> deleted,
        List<CleanupFailure> failed)
    {
        var fullPath = Path.GetFullPath(file);

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(fullPath))
                    return;

                File.SetAttributes(fullPath, FileAttributes.Normal);
                File.Delete(fullPath);

                deleted.Add(fullPath);
                return;
            }
            catch (Exception ex) when (IsRetryableDeleteException(ex) && attempt < _maxAttempts)
            {
                Thread.Sleep(_retryDelay);
            }
            catch (Exception ex) when (IsRetryableDeleteException(ex))
            {
                failed.Add(new CleanupFailure(fullPath, ex.GetType().Name, ex.Message));
                return;
            }
        }
    }

    private static void TryDeleteEmptyDirectories(string root, List<CleanupFailure> failed)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var directory in Directory
                     .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(x => x.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(new CleanupFailure(directory, ex.GetType().Name, ex.Message));
            }
        }
    }

    private static void EnsureDirectoryIsInside(string parent, string child)
    {
        var parentFull = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var childFull = Path.GetFullPath(child)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!childFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsafe cleanup path. Directory '{childFull}' is outside run directory '{parentFull}'.");
        }
    }

    private static bool IsRetryableDeleteException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException;
    }
}

public sealed record CleanupReport(
    int DeletedFiles,
    int FailedFiles,
    IReadOnlyList<string> Deleted,
    IReadOnlyList<CleanupFailure> Failed);

public sealed record CleanupFailure(
    string Path,
    string ErrorType,
    string Message);