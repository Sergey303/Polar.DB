using Polar.DB.Typed.Schema;

namespace Polar.DB.Typed.Runtime;

internal interface IExternalKeyIndexFactory<TRecord>
{
    IExternalKeyIndexTyped<TRecord, TExternalKey> Create<TExternalKey>(FieldScheme field)
        where TExternalKey : IComparable<TExternalKey>;
}
