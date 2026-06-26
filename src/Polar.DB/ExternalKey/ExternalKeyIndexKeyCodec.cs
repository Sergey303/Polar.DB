namespace Polar.DB.ExternalKey;

internal static class ExternalKeyIndexKeyCodec<T>
    where T : IComparable<T>
{
    internal static PType GetStorageType()
    {
        Type type = typeof(T);
        if (type == typeof(int)) return new PType(PTypeEnumeration.integer);
        if (type == typeof(long)) return new PType(PTypeEnumeration.longinteger);
        if (type == typeof(string)) return new PType(PTypeEnumeration.sstring);
        if (type == typeof(Guid)) return new PType(PTypeEnumeration.sstring);
        if (type == typeof(bool)) return new PType(PTypeEnumeration.boolean);

        throw new NotSupportedException($"ExternalKeyIndex does not support key type {type.FullName}.");
    }

    internal static object ToStorage(T key)
    {
        return key is Guid guid ? guid.ToString("N") : key;
    }

    internal static T FromStorage(object value)
    {
        if (typeof(T) == typeof(Guid))
            return (T)(object)Guid.Parse((string)value);

        return (T)value;
    }

    internal static T Cast(IComparable key)
    {
        if (key is T typed) return typed;
        if (typeof(T) == typeof(Guid) && key is string text)
            return (T)(object)Guid.Parse(text);
        if (typeof(T) == typeof(string))
            return (T)(object)Convert.ToString(key)!;
        if (typeof(T) == typeof(bool))
            return (T)(object)Convert.ToBoolean(key);

        return (T)Convert.ChangeType(key, typeof(T));
    }
}
