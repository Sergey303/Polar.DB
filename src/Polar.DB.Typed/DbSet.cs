using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Polar.DB.SchedulingOptimization;
using Polar.DB.Typed.Concurrency;
using Polar.DB.Typed.Runtime;
using Polar.DB.Typed.Schema;
using Polar.Universal;
using RuntimeAppendCollector = Polar.DB.Typed.Runtime.AppendCollector;
using RuntimePrimaryKeyMap = Polar.DB.Typed.Runtime.PrimaryKeyMap;

namespace Polar.DB.Typed;

public sealed class DbSet<T> : IDbSet<T>
{
    private readonly Scheme<T> _scheme;
    private readonly string _tablePath;
    private readonly DbSetGate _gate = new();
    private readonly RuntimeAppendCollector _appendCollector = new();
    private readonly RuntimePrimaryKeyMap _primaryKeyMap = new();
    private readonly InMemoryExternalKeyMap _externalKeyIndexes = new();
    private readonly ActiveSequenceOwner _owner;
    private bool _disposed;

    public DbSet(string rootPath)
        : this(rootPath, _ => { })
    {
    }

    public DbSet(string rootPath, Action<DbSetOptions<T>> configure)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path is required.", nameof(rootPath));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var options = new DbSetOptions<T>();
        configure(options);

        _scheme = SchemeBuilder.Build(options);
        _tablePath = Path.Combine(rootPath, _scheme.StorageName);
        _scheme.SaveOrValidate(_tablePath);
        _owner = new ActiveSequenceOwner(OpenSequence(_tablePath));
        RebuildPrimaryKeyMap();
    }

    public int Count
    {
        get
        {
            ThrowIfDisposed();
            return _gate.Read(() => _primaryKeyMap.Count);
        }
    }

    internal int PendingAppendCount => _appendCollector.Count;
    internal int PrimaryKeyCount => _primaryKeyMap.Count;
    internal int ExternalKeyIndexCount => _externalKeyIndexes.Count;

    public DbSetDiagnostics Diagnostics()
    {
        ThrowIfDisposed();
        return _gate.Read(() => new DbSetDiagnostics(
            _scheme.StorageName,
            _tablePath,
            _scheme.KeyName,
            _scheme.KeyClrTypeName,
            _scheme.FieldNames,
            _scheme.ExternalKeyNames,
            _externalKeyIndexes.BuiltFieldNames,
            _primaryKeyMap.Count,
            _appendCollector.Count));
    }

    public void Append(T value)
    {
        ThrowIfDisposed();
        object record = _scheme.ToRecord(value);
        IComparable key = _scheme.GetRecordKey(record);
        AppendRecords(new[] { new PendingRecord(record, key) });
    }

    public void AddRange(IEnumerable<T> values)
    {
        ThrowIfDisposed();
        if (values == null) throw new ArgumentNullException(nameof(values));

        PendingRecord[] records = values
            .Select(value =>
            {
                object record = _scheme.ToRecord(value);
                IComparable key = _scheme.GetRecordKey(record);
                return new PendingRecord(record, key);
            })
            .ToArray();

        if (records.Length == 0)
            return;

        AppendRecords(records);
    }

    public T GetByKey(IComparable key)
    {
        if (TryGetByKey(key, out T? value))
            return value;

        throw new KeyNotFoundException(
            $"No {typeof(T).Name} record was found for key '{key}'.");
    }

    public bool TryGetByKey(
        IComparable key,
        [MaybeNullWhen(false)] out T value)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));

        IComparable storageKey = _scheme.NormalizeKey(key);
        T? found = _gate.Read(() =>
        {
            return _primaryKeyMap.TryGet(storageKey, out object record)
                ? _scheme.FromRecord(record)
                : default;
        });

        if (found == null)
        {
            value = default;
            return false;
        }

        value = found;
        return true;
    }

    public bool ContainsKey(IComparable key)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));
        IComparable storageKey = _scheme.NormalizeKey(key);
        return _gate.Read(() => _primaryKeyMap.TryGet(storageKey, out _));
    }

    public IReadOnlyList<T> All()
    {
        ThrowIfDisposed();
        return _gate.SequenceRead(() => _owner.Active
            .ElementValues()
            .Where(record => record != null)
            .Select(record => _scheme.FromRecord(record!))
            .ToArray());
    }

    public IReadOnlyList<T> Find<TKey>(Expression<Func<T, TKey>> field, TKey value)
    {
        ThrowIfDisposed();
        string fieldName = ExpressionField.Name(field);
        FieldScheme fieldScheme = _scheme.GetExternalKey(fieldName);

        EnsureExternalKeyIndex(fieldName, fieldScheme);
        object? storageValue = fieldScheme.ToStorageValue(value);

        return _gate.Read(() => _externalKeyIndexes
            .Find(fieldName, storageValue)
            .Select(_scheme.FromRecord)
            .ToArray());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _owner.Dispose();
        _gate.Dispose();
    }

    private void AppendRecords(IReadOnlyList<PendingRecord> records)
    {
        _gate.Write(() =>
        {
            var keysInBatch = new HashSet<IComparable>();
            foreach (PendingRecord item in records)
            {
                if (_primaryKeyMap.TryGet(item.Key, out _))
                    throw new InvalidOperationException($"Duplicate primary key '{item.Key}'.");

                if (!keysInBatch.Add(item.Key))
                    throw new InvalidOperationException($"Duplicate primary key '{item.Key}' inside AddRange batch.");
            }

            foreach (PendingRecord item in records)
                _externalKeyIndexes.ValidateAddToBuiltIndexes(item.Record);

            bool activeSequenceChanged = false;
            try
            {
                foreach (PendingRecord item in records)
                {
                    _owner.AppendElement(item.Record);
                    activeSequenceChanged = true;

                    _primaryKeyMap.Add(item.Key, item.Record);
                    _externalKeyIndexes.AddToBuiltIndexes(item.Record);
                    _appendCollector.Append(item.Record);
                }
            }
            catch
            {
                if (activeSequenceChanged)
                    TryRecoverInMemoryMaps();

                throw;
            }
        });
    }

    private void EnsureExternalKeyIndex(string fieldName, FieldScheme fieldScheme)
    {
        if (_gate.Read(() => _externalKeyIndexes.Has(fieldName)))
            return;

        _gate.Write(() =>
        {
            if (_externalKeyIndexes.Has(fieldName))
                return;

            IEnumerable<object> records = _owner.Active
                .ElementValues()
                .Where(record => record != null)!;

            _externalKeyIndexes.Rebuild(fieldScheme, records);
        });
    }

    private void RebuildPrimaryKeyMap()
    {
        _primaryKeyMap.Rebuild(_owner.Active.ElementValues(), _scheme.GetRecordKey);
    }

    private void TryRecoverInMemoryMaps()
    {
        object[] records = _owner.Active
            .ElementValues()
            .Where(record => record != null)
            .Select(record => record!)
            .ToArray();

        _primaryKeyMap.Rebuild(records, _scheme.GetRecordKey);
        _externalKeyIndexes.RebuildExisting(records);
    }

    private USequence OpenSequence(string tablePath)
    {
        Directory.CreateDirectory(tablePath);
        int fileNumber = 0;

        Stream StreamGen()
        {
            string path = Path.Combine(tablePath, $"data-{fileNumber++:00}.bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        var sequence = new USequence(
            _scheme.RecordType,
            Path.Combine(tablePath, "state.bin"),
            StreamGen,
            _ => false,
            _scheme.GetRecordKey,
            _scheme.HashKey);

        sequence.Refresh();
        return sequence;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DbSet<T>));
    }

    private readonly record struct PendingRecord(object Record, IComparable Key);
}
