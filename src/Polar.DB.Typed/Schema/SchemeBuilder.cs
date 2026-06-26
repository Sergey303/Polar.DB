using System.Globalization;
using System.Reflection;

namespace Polar.DB.Typed.Schema;

internal static class SchemeBuilder
{
    public static Scheme<T> Build<T>(DbSetOptions<T> options)
    {
        Type recordType = typeof(T);
        if (options.KeySelectorValue == null)
        {
            throw new SchemeBuildException(
                recordType,
                fieldName: null,
                $"Primary key is required for {recordType.Name}. Configure it with options.Key(x => x.SomeField).");
        }

        ConstructorInfo constructor = GetConstructor(recordType);
        ParameterInfo[] parameters = constructor.GetParameters();
        PropertyInfo[] properties = recordType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

        FieldScheme[] fields = parameters
            .Select((parameter, index) => CreateField(recordType, parameter, index, properties))
            .ToArray();

        string keyName = ExpressionField.Name(options.KeySelectorValue);
        FieldScheme keyField = fields.FirstOrDefault(field => field.Name == keyName)
            ?? throw new SchemeBuildException(
                recordType,
                keyName,
                $"Key field '{keyName}' was not found on {recordType.Name}.");

        EnsureComparableKey(recordType, keyField);

        string[] externalKeyNames = options.ExternalKeyNames.ToArray();
        foreach (string externalKeyName in externalKeyNames)
        {
            FieldScheme externalKey = fields.FirstOrDefault(field => field.Name == externalKeyName)
                ?? throw new SchemeBuildException(
                    recordType,
                    externalKeyName,
                    $"External key field '{externalKeyName}' was not found on {recordType.Name}.");

            EnsureComparableKey(recordType, externalKey);
        }

        string storageName = options.StorageNameValue ?? DefaultStorageName(recordType);
        var polarRecordType = new PTypeRecord(fields
            .Select(field => new NamedType(field.Name, field.PolarType))
            .ToArray());

        return new Scheme<T>(storageName, polarRecordType, fields, keyField, externalKeyNames, constructor);
    }

    internal static string DefaultStorageName(Type type)
    {
        string name = type.FullName ?? type.Name;
        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            char value = name[i] == '+' ? '.' : name[i];
            chars[i] = invalid.Contains(value) ? '_' : value;
        }

        return new string(chars);
    }

    private static void EnsureComparableKey(Type recordType, FieldScheme field)
    {
        Type typedComparable = typeof(IComparable<>).MakeGenericType(field.ClrType);
        if (!typeof(IComparable).IsAssignableFrom(field.ClrType) ||
            !typedComparable.IsAssignableFrom(field.ClrType))
        {
            throw new SchemeBuildException(
                recordType,
                field.Name,
                $"Key field '{field.Name}' must implement IComparable and IComparable<{field.ClrType.Name}>.");
        }
    }

    private static ConstructorInfo GetConstructor(Type recordType)
    {
        ConstructorInfo? constructor = recordType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .OrderByDescending(item => item.GetParameters().Length)
            .FirstOrDefault();

        if (constructor == null || constructor.GetParameters().Length == 0)
        {
            throw new SchemeBuildException(
                recordType,
                fieldName: null,
                $"{recordType.Name} must have a public constructor with fields, " +
                "for example: public sealed record Person(int Id, string Name).");
        }

        return constructor;
    }

    private static FieldScheme CreateField(
        Type recordType,
        ParameterInfo parameter,
        int index,
        IReadOnlyList<PropertyInfo> properties)
    {
        string parameterName = parameter.Name
            ?? throw new SchemeBuildException(recordType, null, "Constructor parameter has no name.");

        PropertyInfo property = properties.FirstOrDefault(item =>
            string.Equals(item.Name, parameterName, StringComparison.OrdinalIgnoreCase))
            ?? throw new SchemeBuildException(
                recordType,
                parameterName,
                $"Property for constructor parameter '{parameterName}' was not found.");

        FieldStorage storage = ToStorage(recordType, property.Name, property.PropertyType);
        return new FieldScheme(
            property.Name,
            index,
            property.PropertyType,
            storage.PolarType,
            property,
            storage.ToStorage,
            storage.FromStorage);
    }

    private static FieldStorage ToStorage(Type recordType, string fieldName, Type type)
    {
        if (type == typeof(int))
        {
            return new FieldStorage(
                new PType(PTypeEnumeration.integer),
                value => value,
                value => value);
        }

        if (type == typeof(string))
        {
            return new FieldStorage(
                new PType(PTypeEnumeration.sstring),
                value => value,
                value => value);
        }

        if (type == typeof(bool))
        {
            return new FieldStorage(
                new PType(PTypeEnumeration.boolean),
                value => value,
                value => value);
        }

        if (type == typeof(long))
        {
            return new FieldStorage(
                new PType(PTypeEnumeration.sstring),
                value => value == null ? null : ((long)value).ToString(CultureInfo.InvariantCulture),
                value => value == null ? 0L : long.Parse((string)value, CultureInfo.InvariantCulture));
        }

        if (type == typeof(Guid))
        {
            return new FieldStorage(
                new PType(PTypeEnumeration.sstring),
                value => value == null ? null : ((Guid)value).ToString("D"),
                value => value == null ? Guid.Empty : Guid.Parse((string)value));
        }

        throw new SchemeBuildException(
            recordType,
            fieldName,
            $"Field '{fieldName}' on {recordType.Name} has unsupported CLR type " +
            $"'{type.FullName}'. Supported automatic types: int, long, Guid, string, bool.");
    }

    private sealed record FieldStorage(
        PType PolarType,
        Func<object?, object?> ToStorage,
        Func<object?, object?> FromStorage);
}
