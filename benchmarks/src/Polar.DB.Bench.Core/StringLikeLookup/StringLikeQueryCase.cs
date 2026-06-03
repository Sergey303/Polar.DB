using System;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record StringLikeQueryCase(
    string Key,
    StringLikeQueryKind Kind,
    string Pattern,
    string Prefix,
    long ExpectedCount)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key)) throw new ArgumentException("Empty query key.");
        if (string.IsNullOrEmpty(Pattern)) throw new ArgumentException("Empty LIKE pattern.");
        if (string.IsNullOrEmpty(Prefix)) throw new ArgumentException("Empty prefix.");
    }
}
