using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Reads and writes benchmark artifact files.
/// This class is intentionally small: deserialize one file, serialize one file, or load raw run files from an experiment raw folder.
/// </summary>
internal sealed class BenchmarkFileReader
{
    /// <summary>
    /// Reads one JSON file and deserializes it into a typed model.
    /// </summary>
    public async Task<T> ReadAsync<T>(string path)
    {
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Default);
        return value ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from '{path}'.");
    }

    /// <summary>
    /// Writes one typed model as JSON.
    /// Creates parent directory if it does not exist.
    /// Existing behavior is preserved: the file is overwritten by <see cref="File.Create(string)"/>.
    /// </summary>
    public async Task WriteAsync<T>(string path, T value)
    {
        var fullPath = Path.GetFullPath(path);
        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, value, JsonDefaults.Default);
    }

    /// <summary>
    /// Loads immutable raw run artifacts from one raw directory.
    /// Files are sorted by file path to keep deterministic processing order.
    ///
    /// Malformed or obsolete raw artifacts are skipped deliberately. This keeps the report pipeline usable when the
    /// repository contains old experimental raw files written by earlier runner prototypes with a different JSON shape
    /// (for example metrics as an object instead of the canonical RunMetric array).
    /// </summary>
    public async Task<IReadOnlyList<RawRunEntry>> LoadRawRunsAsync(string rawResultsDirectory)
    {
        if (!Directory.Exists(rawResultsDirectory))
        {
            return Array.Empty<RawRunEntry>();
        }

        var files = Directory.GetFiles(rawResultsDirectory, "*.run.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var runs = new List<RawRunEntry>(files.Length);
        var skipped = 0;

        foreach (var file in files)
        {
            try
            {
                var run = await ReadAsync<RunResult>(file);
                runs.Add(new RawRunEntry(run, file));
            }
            catch (Exception ex) when (IsMalformedRawRunException(ex))
            {
                skipped++;
                Console.Error.WriteLine(
                    $"Skipping malformed raw run artifact '{file}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (skipped > 0)
        {
            Console.Error.WriteLine(
                $"Skipped malformed raw run artifacts in '{rawResultsDirectory}': {skipped}.");
        }

        return runs;
    }

    private static bool IsMalformedRawRunException(Exception ex)
    {
        return ex is JsonException or InvalidOperationException or NotSupportedException;
    }
}
