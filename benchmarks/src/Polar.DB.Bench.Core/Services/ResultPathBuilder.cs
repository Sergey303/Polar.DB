using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Polar.DB.Bench.Core.Services;

/// <summary>
/// Naming rules for benchmark artifacts.
/// Raw names are stable factual run files; analyzed and comparison names are derived layers.
/// Derived artifact names are intentionally compact to avoid Windows path-length issues.
/// </summary>
public static class ResultPathBuilder
{
    public static string BuildRawResultPath(
        string rawResultsDirectory,
        string timestampToken,
        string engineKey,
        string? runRole,
        int? sequenceNumber)
    {
        var fileName = !string.IsNullOrWhiteSpace(runRole) && sequenceNumber is not null
            ? $"{timestampToken}__{engineKey}__{runRole}-{sequenceNumber:D2}.run.json"
            : $"{timestampToken}__{engineKey}.run.json";
        return Path.Combine(rawResultsDirectory, fileName);
    }

    public static string BuildAnalyzedResultPath(
        string analyzedResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string engineKey,
        string environmentClass)
    {
        var fileName =
            $"{timestampToken}." +
            $"{CompactToken(experimentKey, 16)}." +
            $"{CompactToken(datasetProfileKey, 16)}." +
            $"{CompactToken(engineKey, 12)}." +
            $"{CompactToken(environmentClass, 12)}.eval.json";

        return Path.Combine(analyzedResultsDirectory, fileName);
    }

    public static string BuildComparisonResultPath(
        string comparisonResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string fairnessProfileKey)
    {
        var fileName =
            $"{timestampToken}." +
            $"{CompactToken(experimentKey, 16)}." +
            $"{CompactToken(datasetProfileKey, 16)}." +
            $"{CompactToken(fairnessProfileKey, 12)}.comparison.json";

        return Path.Combine(comparisonResultsDirectory, fileName);
    }

    public static string BuildComparisonSeriesResultPath(
        string comparisonResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string fairnessProfileKey,
        string comparisonSetId)
    {
        var fileName =
            $"{timestampToken}." +
            $"{CompactToken(experimentKey, 16)}." +
            $"{CompactToken(datasetProfileKey, 16)}." +
            $"{CompactToken(fairnessProfileKey, 12)}." +
            $"{CompactToken(comparisonSetId, 16)}.comparison-series.json";

        return Path.Combine(comparisonResultsDirectory, fileName);
    }

    private static string CompactToken(string value, int prefixLength)
    {
        var normalized = NormalizeFileToken(value);
        if (normalized.Length <= prefixLength)
        {
            return normalized;
        }

        var prefix = normalized[..prefixLength];
        var hash = ComputeShortHash(normalized);
        return $"{prefix}-{hash}";
    }

    private static string NormalizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty";
        }

        var trimmed = value.Trim().ToLowerInvariant();
        var buffer = new StringBuilder(trimmed.Length);

        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(ch);
            }
            else if (ch is '-' or '_' or '.')
            {
                buffer.Append('-');
            }
            else
            {
                buffer.Append('-');
            }
        }

        var normalized = buffer.ToString();
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "empty" : normalized;
    }

    private static string ComputeShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..6]).ToLowerInvariant();
    }
}