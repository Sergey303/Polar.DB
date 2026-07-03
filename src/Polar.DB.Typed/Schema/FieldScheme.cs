using System.Reflection;

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

    public object? ReadStorageValue(object storageRecord)
    {
        if (storageRecord == null) throw new ArgumentNullException(nameof(storageRecord));
        return ((object[])storageRecord)[Index];
    }

    public TField ReadClrValue<TField>(object storageRecord)
    {
        EnsureClrType<TField>();

        object? value = FromStorageValue(ReadStorageValue(storageRecord));
        if (value is TField typed)
            return typed;

        if (value == null && default(TField) == null)
            return default!;

        throw new InvalidOperationException(
            $"Field '{Name}' expected CLR value type '{typeof(TField).FullName}', " +
            $"but storage conversion returned '{value?.GetType().FullName ?? "<null>"}'.");
    }

    public IEnumerable<TField> ReadSingleClrValue<TField>(object storageRecord)
    {
        TField value = ReadClrValue<TField>(storageRecord);
        if (value == null)
            yield break;

        yield return value;
    }

    public void EnsureClrType<TField>()
    {
        if (ClrType != typeof(TField))
        {
            throw new ArgumentException(
                $"Field '{Name}' expects key type '{ClrType.FullName}', " +
                $"but got '{typeof(TField).FullName}'.");
        }
    }

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
