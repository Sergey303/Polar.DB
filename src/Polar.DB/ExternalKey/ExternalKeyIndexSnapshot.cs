namespace Polar.DB.ExternalKey;

internal sealed class ExternalKeyIndexSnapshot<T>
    where T : IComparable<T>
{
    internal static readonly ExternalKeyIndexSnapshot<T> Empty =
        new(Array.Empty<T>(), Array.Empty<long>());

    internal ExternalKeyIndexSnapshot(T[] keys, long[] offsets)
    {
        Keys = keys;
        Offsets = offsets;
    }

    internal T[] Keys { get; }
    internal long[] Offsets { get; }
}
