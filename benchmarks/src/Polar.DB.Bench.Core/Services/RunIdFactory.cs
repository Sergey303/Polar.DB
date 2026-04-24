using System;
using System.Security.Cryptography;
using System.Text;

namespace Polar.DB.Bench.Core.Services;

public static class RunIdFactory
{
    public static string Create(
        string experimentKey,
        string datasetProfileKey,
        string engineKey,
        string environmentClass)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");

        return $"{timestamp}__" +
               $"{CompactToken(experimentKey, 16)}__" +
               $"{CompactToken(datasetProfileKey, 16)}__" +
               $"{CompactToken(engineKey, 12)}__" +
               $"{CompactToken(environmentClass, 8)}";
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