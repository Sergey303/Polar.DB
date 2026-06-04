namespace Polar.DB.ExternalKey;

internal interface IExternalKeyIndex
{
    IEnumerable<object> GetManyByValue(IComparable value);

    Task CompactAsync(CancellationToken cancellationToken);
}
