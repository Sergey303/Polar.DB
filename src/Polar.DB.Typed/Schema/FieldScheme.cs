using System.Reflection;
using Polar.DB;

namespace Polar.DB.Typed.Schema;

internal sealed class FieldScheme
{
    private readonly Func<object?, object?> _toStorage;
    private readonly Func<object?, object?> _fromStorage;

    public FieldScheme(
        string name,
        int index,
        Type clrType,
        PType polarType,
        PropertyInfo property,
        Func<object?, object?> toStorage,
        Func<object?, object?> fromStorage)
    {
        Name = name;
        Index = index;
        ClrType = clrType;
        PolarType = polarType;
        Property = property;
        _toStorage = toStorage;
        _fromStorage = fromStorage;
    }

    public string Name { get; }
    public int Index { get; }
    public Type ClrType { get; }
    public PType PolarType { get; }
    public PropertyInfo Property { get; }

    public object? ToStorageValue(object? value) => _toStorage(value);

    public object? FromStorageValue(object? value) => _fromStorage(value);

    public IComparable ToComparableStorageKey(IComparable key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (!ClrType.IsInstanceOfType(key))
        {
            throw new ArgumentException(
                $"Field '{Name}' expects key type '{ClrType.FullName}', " +
                $"but got '{key.GetType().FullName}'.",
                nameof(key));
        }

        object? storageValue = ToStorageValue(key);
        if (storageValue is IComparable comparable)
            return comparable;

        throw new ArgumentException(
            $"Key value for field '{Name}' must be comparable after storage conversion.",
            nameof(key));
    }
}
