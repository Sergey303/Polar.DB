using System.Reflection;
using System.Text.Json;

namespace Polar.DB.Typed.Schema;

internal sealed class Scheme<T>
{
    private readonly ConstructorInfo _constructor;
    private readonly FieldScheme _keyField;
    private readonly HashSet<string> _externalKeyNames;

    public Scheme(
        string storageName,
        PType recordType,
        IReadOnlyList<FieldScheme> fields,
        FieldScheme keyField,
        IReadOnlyCollection<string> externalKeyNames,
        ConstructorInfo constructor)
    {
        StorageName = storageName;
        RecordType = recordType;
        Fields = fields;
        _keyField = keyField;
        _externalKeyNames = new HashSet<string>(externalKeyNames, StringComparer.Ordinal);
        _constructor = constructor;
    }

    public string StorageName { get; }
    public PType RecordType { get; }
    public IReadOnlyList<FieldScheme> Fields { get; }
    public string KeyName => _keyField.Name;
    public string KeyClrTypeName => _keyField.ClrType.FullName ?? _keyField.ClrType.Name;

    public IReadOnlyList<string> FieldNames => Fields
        .Select(item => item.Name)
        .ToArray();

    public IReadOnlyList<string> ExternalKeyNames => Fields
        .Where(item => _externalKeyNames.Contains(item.Name))
        .Select(item => item.Name)
        .ToArray();

    public object ToRecord(T value)
    {
        object[] record = new object[Fields.Count];
        foreach (FieldScheme field in Fields)
            record[field.Index] = field.ToStorageValue(field.Property.GetValue(value))!;
        return record;
    }

    public T FromRecord(object record)
    {
        object[] fields = (object[])record;
        object?[] args = new object?[Fields.Count];
        foreach (FieldScheme field in Fields)
            args[field.Index] = field.FromStorageValue(fields[field.Index]);
        return (T)_constructor.Invoke(args);
    }

    public IComparable GetRecordKey(object record)
    {
        object key = ((object[])record)[_keyField.Index];
        return (IComparable)key;
    }

    public IComparable NormalizeKey(IComparable key) => _keyField.ToComparableStorageKey(key);

    public int HashKey(IComparable key) => StableHash.OfKey(key);

    public FieldScheme GetField(string name)
    {
        foreach (FieldScheme field in Fields)
        {
            if (string.Equals(field.Name, name, StringComparison.Ordinal))
                return field;
        }

        throw new InvalidOperationException($"Field '{name}' is not part of {typeof(T).Name}.");
    }

    public FieldScheme GetExternalKey(string name)
    {
        FieldScheme field = GetField(name);
        if (_externalKeyNames.Contains(name))
            return field;

        throw new InvalidOperationException(
            $"Field '{name}' is not configured as an external key for {typeof(T).Name}. " +
            $"Add options.ExternalKey(x => x.{name}) in the DbSet constructor.");
    }

    public SchemeSnapshot Snapshot() => new(
        typeof(T).FullName ?? typeof(T).Name,
        StorageName,
        _keyField.Name,
        Fields.Select(item => new SchemeFieldSnapshot(
            item.Name,
            item.ClrType.FullName ?? item.ClrType.Name,
            item.PolarType.ToString() ?? item.PolarType.GetType().Name,
            item.Index,
            item.Index == _keyField.Index)).ToArray());

    public void SaveOrValidate(string tablePath)
    {
        Directory.CreateDirectory(tablePath);
        string schemaPath = Path.Combine(tablePath, "schema.json");
        SchemeSnapshot current = Snapshot();
        string currentJson = ToJson(current);

        if (!File.Exists(schemaPath))
        {
            SchemaJsonFile.WriteAtomic(schemaPath, currentJson);
            return;
        }

        string existingJson = SchemaJsonFile.Read(schemaPath);
        if (string.Equals(existingJson.Trim(), currentJson.Trim(), StringComparison.Ordinal))
            return;

        SchemeSnapshot stored = ReadStoredSnapshot(existingJson, tablePath, schemaPath);
        string detail = SchemeSnapshotComparer.DescribeDifference(stored, current);
        throw new SchemeCompatibilityException(tablePath, schemaPath, detail);
    }

    private static SchemeSnapshot ReadStoredSnapshot(
        string json,
        string tablePath,
        string schemaPath)
    {
        try
        {
            return JsonSerializer.Deserialize<SchemeSnapshot>(json)
                ?? throw new JsonException("Stored scheme snapshot is empty.");
        }
        catch (JsonException ex)
        {
            throw new SchemeCompatibilityException(
                tablePath,
                schemaPath,
                $"Stored schema.json is not valid JSON: {ex.Message}");
        }
    }

    private static string ToJson(SchemeSnapshot snapshot)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(snapshot, options) + Environment.NewLine;
    }
}
