using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Polar.DB.Bench.Exec.ExternalNuget;

internal static partial class PolarDbNugetVersionInference
{
    private static readonly Regex EngineKeyVersionRegex = new(
        @"polar-db-(?<version>[0-9]+(?:\.[0-9]+){1,3}(?:[-a-zA-Z0-9.]+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SemVerRegex = new(
        @"(?<![0-9A-Za-z.])(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:[-a-zA-Z0-9.]+)?)(?![0-9A-Za-z.])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? TryInferPackageVersion(string? engineKeyOrText)
    {
        if (string.IsNullOrWhiteSpace(engineKeyOrText))
        {
            return null;
        }

        var engineKeyMatch = EngineKeyVersionRegex.Match(engineKeyOrText);
        if (engineKeyMatch.Success)
        {
            var version = engineKeyMatch.Groups["version"].Value.Trim();
            if (IsUsefulVersion(version))
            {
                return version;
            }
        }

        var semVerMatch = SemVerRegex.Match(engineKeyOrText);
        if (semVerMatch.Success)
        {
            var version = semVerMatch.Groups["version"].Value.Trim();
            if (IsUsefulVersion(version))
            {
                return version;
            }
        }

        return null;
    }

    private static bool IsUsefulVersion(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.Equals("current", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals("nuget", StringComparison.OrdinalIgnoreCase) &&
               value.Any(char.IsDigit);
    }
}
