namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Small helpers for common comparison value handling.
/// </summary>
internal static class ComparisonValueHelpers
{
    public static string ResolveSharedOrMixed(IEnumerable<string> values)
    {
        var list = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return list.Length == 1 ? list[0] : "mixed";
    }

    public static string ToFileToken(string value)
    {
        var chars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(chars);
    }
}
