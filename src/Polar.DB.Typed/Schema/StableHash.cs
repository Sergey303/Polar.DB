namespace Polar.DB.Typed.Schema;

internal static class StableHash
{
    public static int OfKey(IComparable key)
    {
        if (key is int intValue) return OfInt32(intValue);
        if (key is string text) return OfString(text);
        return key.GetHashCode();
    }

    public static int OfInt32(int value) => value;

    public static int OfString(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));

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
