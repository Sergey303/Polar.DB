using System;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record PrefixBounds(string LowerInclusive, string? UpperExclusive);

public static class PrefixBoundsBuilder
{
    public static PrefixBounds ForPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("Prefix must not be empty.", nameof(prefix));
        }

        var upper = NextOrdinalString(prefix);
        return new PrefixBounds(prefix, upper);
    }

    private static string? NextOrdinalString(string value)
    {
        var chars = value.ToCharArray();

        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] == char.MaxValue) continue;
            chars[i]++;
            return new string(chars, 0, i + 1);
        }

        return null;
    }
}
