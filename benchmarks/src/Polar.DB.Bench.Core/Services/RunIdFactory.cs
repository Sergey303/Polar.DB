using System;
using System.Linq;
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
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var engineToken = ResultPathBuilder.CompactEngineKey(engineKey);

        var identityHash = ComputeShortHash(
            JoinIdentity(experimentKey, datasetProfileKey, engineKey, environmentClass),
            byteCount: 5);

        var nonce = Convert
            .ToHexString(RandomNumberGenerator.GetBytes(3))
            .ToLowerInvariant();

        return $"r-{timestamp}-{engineToken}-{identityHash}-{nonce}";
    }

    private static string JoinIdentity(params string[] values)
    {
        return string.Join("|", values.Select(NormalizeFileToken));
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
            buffer.Append(char.IsLetterOrDigit(ch) ? ch : '-');
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