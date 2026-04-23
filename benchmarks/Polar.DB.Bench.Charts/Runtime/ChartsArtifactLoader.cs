using System.Text.Json;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Loads report input artifacts from disk.
/// It reads already-produced JSON artifacts and does not compute any metrics itself.
/// </summary>
internal sealed class ChartsArtifactLoader
{
    /// <summary>
    /// Loads analyzed result artifacts from one directory.
    /// </summary>
    public Task<IReadOnlyList<AnalyzedResult>> LoadAnalyzedResultsAsync(string directory)
    {
        return LoadAsync<AnalyzedResult>(directory, "*.eval.json");
    }

    /// <summary>
    /// Loads stage4 comparison-series artifacts from one directory.
    /// </summary>
    public Task<IReadOnlyList<CrossEngineComparisonSeriesResult>> LoadSeriesComparisonsAsync(string directory)
    {
        return LoadAsync<CrossEngineComparisonSeriesResult>(directory, "*.comparison-series.json");
    }

    /// <summary>
    /// Loads legacy single-run comparison artifacts from one directory.
    /// </summary>
    public Task<IReadOnlyList<CrossEngineComparisonResult>> LoadLegacyComparisonsAsync(string directory)
    {
        return LoadAsync<CrossEngineComparisonResult>(directory, "*.comparison.json");
    }

    private static async Task<IReadOnlyList<T>> LoadAsync<T>(string directory, string pattern)
    {
        var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<T>(files.Length);
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var value = await JsonAliasNormalizer.DeserializeAsync<T>(stream, JsonDefaults.Default);
            if (value is not null)
            {
                results.Add(value);
            }
        }

        return results;
    }
}
