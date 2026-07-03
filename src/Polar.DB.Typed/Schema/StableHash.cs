namespace Polar.DB.Typed.Schema;

internal static class StableHash
{
    public static int OfKey(IComparable key)
    {
        if (key is int intValue) return intValue;
        if (key is string text) return OfString(text);
        return key.GetHashCode();
    }

    private static int OfString(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (char ch in text)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return hash;
        }
    }
}
