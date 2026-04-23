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
    /// Existing behavior is preserved: the file is overwritten by <see cref="File.Create(string)"/>.
    /// </summary>
    public async Task WriteAsync<T>(string path, T value)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonDefaults.Default);
    }

    /// <summary>
    /// Loads all immutable raw run artifacts from one raw directory.
    /// Files are sorted by file path to keep deterministic processing order.
    /// </summary>
    public async Task<IReadOnlyList<RawRunEntry>> LoadRawRunsAsync(string rawResultsDirectory)
    {
        var files = Directory.GetFiles(rawResultsDirectory, "*.run.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var runs = new List<RawRunEntry>(files.Length);
        foreach (var file in files)
        {
            var run = await ReadAsync<RunResult>(file);
            runs.Add(new RawRunEntry(run, file));
        }

        return runs;
    }
}
