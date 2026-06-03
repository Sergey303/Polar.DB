namespace Polar.DB.ExternalKey;

public interface IExternalKeyIndex
{
    IEnumerable<object> GetManyByValue(IComparable value);
}
