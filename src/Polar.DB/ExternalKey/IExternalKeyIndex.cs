using Polar.Universal;

namespace Polar.DB.ExternalKey;

public interface IExternalKeyIndex : IUIndex
{
    IEnumerable<object> GetManyByValue(IComparable value);
}

public interface IExternalKeyIndex<TKey> : IExternalKeyIndex
    where TKey : IComparable<TKey>
{
    IEnumerable<object> GetManyByValue(TKey value);
}
