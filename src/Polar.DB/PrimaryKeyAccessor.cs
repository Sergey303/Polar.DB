using System.Linq.Expressions;

namespace Polar.Universal;

internal interface IPrimaryKeyAccessor
{
    IComparable GetKey(object value);
    int Hash(IComparable key);
}

internal sealed class TypedPrimaryKeyAccessor<TKey> : IPrimaryKeyAccessor
    where TKey : IComparable, IComparable<TKey>, IEquatable<TKey>
{
    private readonly Func<object, TKey> _selector;
    private readonly Func<TKey, int> _hasher;

    public TypedPrimaryKeyAccessor(
        Expression<Func<object, TKey>> keyExpression,
        Func<TKey, int>? hasher)
    {
        if (keyExpression == null) throw new ArgumentNullException(nameof(keyExpression));
        _selector = keyExpression.Compile();
        _hasher = hasher ?? PrimaryKeyHasherDefaults<TKey>.Create();
    }

    IComparable IPrimaryKeyAccessor.GetKey(object value) => _selector(value);
    int IPrimaryKeyAccessor.Hash(IComparable key) => _hasher((TKey)key);
}

internal sealed class DelegatePrimaryKeyAccessor : IPrimaryKeyAccessor
{
    private readonly Func<object, IComparable> _selector;
    private readonly Func<IComparable, int> _hasher;

    public DelegatePrimaryKeyAccessor(
        Func<object, IComparable> selector,
        Func<IComparable, int> hasher)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    public IComparable GetKey(object value) => _selector(value);
    public int Hash(IComparable key) => _hasher(key);
}

internal static class PrimaryKeyHasherDefaults<TKey>
{
    public static Func<TKey, int> Create()
    {
        if (typeof(TKey) == typeof(int))
            return (Func<TKey, int>)(object)(Func<int, int>)(value => value);
        if (typeof(TKey) == typeof(long))
            return (Func<TKey, int>)(object)(Func<long, int>)(value => unchecked((int)(value ^ (value >> 32))));
        if (typeof(TKey) == typeof(Guid))
            return (Func<TKey, int>)(object)(Func<Guid, int>)StableGuidHash;
        if (typeof(TKey) == typeof(string))
            return (Func<TKey, int>)(object)(Func<string, int>)StableStringHash;
        throw new NotSupportedException(
            $"Primary key type '{typeof(TKey)}' has no default stable hash. Pass an explicit hasher to SetPrimaryKey.");
    }

    private static int StableGuidHash(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!value.TryWriteBytes(bytes))
            throw new InvalidOperationException("Could not write Guid bytes for stable primary-key hashing.");
        unchecked
        {
            var hash = (int)2166136261;
            foreach (byte valueByte in bytes)
            {
                hash ^= valueByte;
                hash *= 16777619;
            }
            return hash;
        }
    }

    private static int StableStringHash(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        unchecked
        {
            var hash = (int)2166136261;
            foreach (char ch in value)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
