using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Polar.DB.Typed;

public interface IDbSet<T> : IDisposable
{
    int Count { get; }

    void Append(T value);

    void AddRange(IEnumerable<T> values);

    T GetByKey(IComparable key);

    bool TryGetByKey(
        IComparable key,
        [MaybeNullWhen(false)] out T value);

    bool ContainsKey(IComparable key);

    IReadOnlyList<T> All();

    IReadOnlyList<T> Find<TKey>(
        Expression<Func<T, TKey>> field,
        TKey value)
        where TKey : IComparable<TKey>;
}
