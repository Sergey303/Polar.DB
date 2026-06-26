using System.Linq.Expressions;
using Polar.DB.Typed.Schema;

namespace Polar.DB.Typed;

public sealed class DbSetOptions<T>
{
    private readonly HashSet<string> _externalKeyNames = new(StringComparer.Ordinal);

    internal string? StorageNameValue { get; private set; }
    internal LambdaExpression? KeySelectorValue { get; private set; }
    internal IReadOnlyCollection<string> ExternalKeyNames => _externalKeyNames;

    public DbSetOptions<T> Name(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Storage name is required.", nameof(name));

        StorageNameValue = name;
        return this;
    }

    public DbSetOptions<T> UseKey<TKey>(Expression<Func<T, TKey>> field)
        where TKey : IComparable<TKey>
    {
        if (field == null) throw new ArgumentNullException(nameof(field));
        KeySelectorValue = field;
        return this;
    }

    public DbSetOptions<T> UseExternalKey<TKey>(Expression<Func<T, TKey>> field)
        where TKey : IComparable<TKey>
    {
        if (field == null) throw new ArgumentNullException(nameof(field));
        _externalKeyNames.Add(ExpressionField.Name(field));
        return this;
    }
}
