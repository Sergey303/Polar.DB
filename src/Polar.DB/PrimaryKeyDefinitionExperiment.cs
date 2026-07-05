using System.Linq.Expressions;

namespace Polar.Universal;

internal interface IPrimaryKeyDefinition
{
    Type KeyType { get; }
    Func<object, IComparable> LegacyKeySelector { get; }
    Func<IComparable, int> LegacyHasher { get; }
}

internal sealed class PrimaryKeyDefinition<TKey> : IPrimaryKeyDefinition
    where TKey : struct, IComparable, IComparable<TKey>, IEquatable<TKey>
{
    private readonly Func<object, TKey> selector;
    private readonly Func<TKey, int> hasher;

    public PrimaryKeyDefinition(
        Expression<Func<object, TKey>> keyExpression,
        Func<TKey, int>? hasher)
    {
        if (keyExpression == null) throw new ArgumentNullException(nameof(keyExpression));

        selector = keyExpression.Compile();
        this.hasher = hasher ?? PrimaryKeyHasherDefaults<TKey>.Create();
        IsScalarIdentity = IsScalarIdentityExpression(keyExpression);

        LegacyKeySelector = value => selector(value);
        LegacyHasher = key => this.hasher((TKey)key);
    }

    public Type KeyType => typeof(TKey);
    public bool IsScalarIdentity { get; }
    public Func<object, IComparable> LegacyKeySelector { get; }
    public Func<IComparable, int> LegacyHasher { get; }

    public TKey GetKey(object value) => selector(value);
    public int Hash(TKey key) => hasher(key);

    private static bool IsScalarIdentityExpression(Expression<Func<object, TKey>> expression)
    {
        if (expression.Body is not UnaryExpression conversion) return false;
        if (conversion.NodeType != ExpressionType.Convert && conversion.NodeType != ExpressionType.ConvertChecked)
            return false;

        return ReferenceEquals(conversion.Operand, expression.Parameters[0]) &&
               conversion.Type == typeof(TKey);
    }
}

internal static class PrimaryKeyHasherDefaults<TKey>
    where TKey : struct
{
    public static Func<TKey, int> Create()
    {
        if (typeof(TKey) == typeof(int))
            return key => (int)(object)key;

        if (typeof(TKey) == typeof(long))
            return key =>
            {
                var value = (long)(object)key;
                return unchecked((int)(value ^ (value >> 32)));
            };

        if (typeof(TKey) == typeof(Guid))
            return key => StableGuidHash((Guid)(object)key);

        throw new NotSupportedException(
            $"Primary key type '{typeof(TKey)}' has no default stable hash. Pass an explicit hasher to SetPrimaryKey.");
    }

    private static int StableGuidHash(Guid value)
    {
        unchecked
        {
            var bytes = value.ToByteArray();
            var hash = (int)2166136261;
            for (var i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619;
            }

            return hash;
        }
    }
}
