using Polar.DB.ExternalKey;
using Polar.DB.Typed.Schema;
using Polar.Universal;

namespace Polar.DB.Typed.Runtime;

internal sealed class ExternalKeyIndexFactory<TRecord> : IExternalKeyIndexFactory<TRecord>
{
    private readonly string _tablePath;
    private readonly USequence _sequence;
    private readonly Func<object, TRecord> _fromStorageRecord;

    public ExternalKeyIndexFactory(
        string tablePath,
        USequence sequence,
        Func<object, TRecord> fromStorageRecord)
    {
        if (string.IsNullOrWhiteSpace(tablePath))
            throw new ArgumentException("Table path is required.", nameof(tablePath));

        _tablePath = tablePath;
        _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _fromStorageRecord = fromStorageRecord ?? throw new ArgumentNullException(nameof(fromStorageRecord));
    }

    public IExternalKeyIndexTyped<TRecord, TExternalKey> Create<TExternalKey>(FieldScheme field)
        where TExternalKey : IComparable<TExternalKey>
    {
        if (field == null) throw new ArgumentNullException(nameof(field));
        field.EnsureClrType<TExternalKey>();

        IExternalKeyIndex<TExternalKey> storageIndex = new ExternalKeyIndex<TExternalKey>(
            CreateFieldStreamGenerator(field.Name),
            _sequence,
            field.ReadSingleClrValue<TExternalKey>);

        return new ExternalKeyIndexTyped<TRecord, TExternalKey>(
            field.Name,
            storageIndex,
            _fromStorageRecord,
            field.ReadClrValue<TExternalKey>);
    }

    private Func<Stream> CreateFieldStreamGenerator(string fieldName)
    {
        string safeFieldName = MakeSafeFileName(fieldName);
        int fileNumber = 0;

        return () =>
        {
            string path = Path.Combine(_tablePath, $"external-{safeFieldName}-{fileNumber++:00}.bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        };
    }

    private static string MakeSafeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = new char[value.Length];
        for (int i = 0; i < value.Length; i++)
            chars[i] = invalid.Contains(value[i]) ? '_' : value[i];

        return new string(chars);
    }
}
