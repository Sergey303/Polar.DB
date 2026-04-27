using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Polar.DB.Bench.Core.Services;

/// <summary>
/// Naming rules for benchmark artifacts.
/// Raw names are stable factual run files; analyzed and comparison names are derived layers.
/// Names are intentionally short because benchmark experiment paths are already long on Windows.
/// Human-readable identity is stored inside JSON, not duplicated in file names.
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
        var safeTimestamp = NormalizeTimestampToken(timestampToken);
        var engineToken = CompactEngineKey(engineKey);
        var fileName = !string.IsNullOrWhiteSpace(runRole) && sequenceNumber is not null
            ? $"r.{safeTimestamp}.{engineToken}.{CompactRunRole(runRole)}{sequenceNumber.Value:D2}.json"
            : $"r.{safeTimestamp}.{engineToken}.json";

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
        var safeTimestamp = NormalizeTimestampToken(timestampToken);
        var fileName =
            $"a.{safeTimestamp}." +
            $"{CompactEngineKey(engineKey)}." +
            $"{HashToken(experimentKey, datasetProfileKey, environmentClass)}.json";

        return Path.Combine(analyzedResultsDirectory, fileName);
    }

    public static string BuildComparisonResultPath(
        string comparisonResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string fairnessProfileKey)
    {
        var safeTimestamp = NormalizeTimestampToken(timestampToken);
        var fileName =
            $"c.{safeTimestamp}." +
            $"{HashToken(experimentKey, datasetProfileKey, fairnessProfileKey)}.json";

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
        var safeTimestamp = NormalizeTimestampToken(timestampToken);
        var fileName =
            $"cs.{safeTimestamp}." +
            $"{CompactFileToken(comparisonSetId, 10)}." +
            $"{HashToken(experimentKey, datasetProfileKey, fairnessProfileKey, comparisonSetId)}.json";

        return Path.Combine(comparisonResultsDirectory, fileName);
    }

    public static string CompactEngineKey(string engineKey)
    {
        var normalized = NormalizeFileToken(engineKey);
        return normalized switch
        {
            "polar-db-current" => "pdbc",
            "polar-db-2-1-0" => "pdb210",
            "polar-db-2-1-1" => "pdb211",
            "polar-db" => "pdb",
            "sqlite" => "sqlite",
            "synthetic" => "syn",
            _ => CompactFileToken(normalized, 8)
        };
    }

    public static string CompactRunRole(string runRole)
    {
        var normalized = NormalizeFileToken(runRole);
        return normalized switch
        {
            "warmup" => "w",
            "measured" => "m",
            _ => CompactFileToken(normalized, 4)
        };
    }

    public static string CompactFileToken(string value, int prefixLength)
    {
        var normalized = NormalizeFileToken(value);
        if (normalized.Length <= prefixLength)
        {
            return normalized;
        }

        var prefix = normalized[..prefixLength];
        var hash = ComputeShortHash(normalized, 4);
        return $"{prefix}-{hash}";
    }

    private static string HashToken(params string?[] values)
    {
        var joined = string.Join("|", values.Select(value => NormalizeFileToken(value ?? string.Empty)));
        return ComputeShortHash(joined, 5);
    }

    private static string NormalizeTimestampToken(string timestampToken)
    {
        if (string.IsNullOrWhiteSpace(timestampToken))
        {
            return DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
        }

        // Supports both old tokens like 2026-04-25T19-18-12Z and new tokens like 20260425T191812Z.
        var normalized = timestampToken
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal);

        normalized = NormalizeFileToken(normalized).Replace("-", string.Empty, StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(normalized) ? DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ") : normalized;
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

    private static string ComputeShortHash(string value, int byteCount)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..byteCount]).ToLowerInvariant();
    }
}
