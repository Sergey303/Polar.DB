namespace Polar.DB
{
    /// <summary>
    /// Provides named-field access for record values represented as <c>object[]</c> under a <see cref="PTypeRecord"/> schema.
    /// </summary>
    /// <remarks>
    /// The accessor does not allocate wrappers per read/write operation and can be reused for many records of the same schema.
    /// </remarks>
    public sealed class RecordAccessor
    {
        private readonly PTypeRecord _recordType;
        private readonly Dictionary<string, int> _fieldIndexes;

        /// <summary>
        /// Creates an accessor for a specific record schema.
        /// </summary>
        /// <param name="recordType">Record schema defining field names, order and types.</param>
        /// <exception cref="ArgumentNullException"><paramref name="recordType"/> is <see langword="null"/>.</exception>
        public RecordAccessor(PTypeRecord recordType)
        {
            _recordType = recordType ?? throw new ArgumentNullException(nameof(recordType));
            _fieldIndexes = recordType.Fields
                .Select((field, index) => new { field.Name, Index = index })
                .ToDictionary(x => x.Name, x => x.Index, StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets the record schema associated with this accessor.
        /// </summary>
        public PTypeRecord RecordType => _recordType;

        /// <summary>
        /// Gets the number of fields defined by the schema.
        /// </summary>
        public int FieldCount => _recordType.Fields.Length;

        /// <summary>
        /// Enumerates schema field names in schema order.
        /// </summary>
        public IEnumerable<string> FieldNames => _recordType.Fields.Select(f => f.Name);

        /// <summary>
        /// Checks whether the schema contains a field with the specified name.
        /// </summary>
        /// <param name="fieldName">Field name to check.</param>
        /// <returns><see langword="true"/> if the field exists; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fieldName"/> is <see langword="null"/>.</exception>
        public bool HasField(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            return _fieldIndexes.ContainsKey(fieldName);
        }

        /// <summary>
        /// Gets a zero-based field index for the specified field name.
        /// </summary>
        /// <param name="fieldName">Field name.</param>
        /// <returns>Zero-based index of the field in the record array.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fieldName"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The field name is not present in the schema.</exception>
        public int GetIndex(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            if (!_fieldIndexes.TryGetValue(fieldName, out int index))
                throw new ArgumentException($"Unknown field '{fieldName}'.", nameof(fieldName));

            return index;
        }

        /// <summary>
        /// Gets the schema type of a field by name.
        /// </summary>
        /// <param name="fieldName">Field name.</param>
        /// <returns>Type descriptor declared for the field.</returns>
        public PType GetFieldType(string fieldName)
        {
            return _recordType.Fields[GetIndex(fieldName)].Type;
        }

        /// <summary>
        /// Creates an empty record array sized for the current schema.
        /// </summary>
        public object[] CreateRecord()
        {
            return new object[FieldCount];
        }

        /// <summary>
        /// Validates and returns the provided record values array.
        /// </summary>
        /// <param name="values">Field values in schema order.</param>
        /// <returns>The same <paramref name="values"/> array.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Value count does not match schema field count.</exception>
        public object[] CreateRecord(params object[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length != FieldCount)
                throw new ArgumentException(
                    $"Record field count mismatch. Expected {FieldCount}, got {values.Length}.",
                    nameof(values));

            return values;
        }

        /// <summary>
        /// Validates that an object is a record array compatible with the schema.
        /// </summary>
        /// <param name="record">Object expected to be an <c>object[]</c> of schema length.</param>
        /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Object is not a compatible record array.</exception>
        public void ValidateShape(object record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (record is not object[] arr)
                throw new ArgumentException("Record value must be object[].", nameof(record));
            if (arr.Length != FieldCount)
                throw new ArgumentException(
                    $"Record field count mismatch. Expected {FieldCount}, got {arr.Length}.",
                    nameof(record));
        }

        /// <summary>
        /// Gets a field value by name.
        /// </summary>
        /// <param name="record">Record array.</param>
        /// <param name="fieldName">Field name.</param>
        /// <returns>Stored field value.</returns>
        public object Get(object record, string fieldName)
        {
            ValidateShape(record);
            return ((object[])record)[GetIndex(fieldName)];
        }

        /// <summary>
        /// Gets a field value by name and casts it to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Expected value type.</typeparam>
        /// <param name="record">Record array.</param>
        /// <param name="fieldName">Field name.</param>
        /// <returns>Typed field value.</returns>
        public T Get<T>(object record, string fieldName)
        {
            return (T)Get(record, fieldName);
        }

        /// <summary>
        /// Sets a field value by name.
        /// </summary>
        /// <param name="record">Record array.</param>
        /// <param name="fieldName">Field name.</param>
        /// <param name="value">New field value.</param>
        public void Set(object record, string fieldName, object value)
        {
            _ = value ?? throw new ArgumentNullException(nameof(value));
            ValidateShape(record);
            ((object[])record)[GetIndex(fieldName)] = value;
        }

        /// <summary>
        /// Tries to read a field value without throwing for unknown fields or invalid record shape.
        /// </summary>
        /// <param name="record">Record array candidate.</param>
        /// <param name="fieldName">Field name.</param>
        /// <param name="value">Field value on success; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when value was read; otherwise <see langword="false"/>.</returns>
        public bool TryGet(object record, string fieldName, out object? value)
        {
            _ = record ?? throw new ArgumentNullException(nameof(record));
            _ = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
            value = null;
            if (record is not object[] arr) return false;
            if (!_fieldIndexes.TryGetValue(fieldName, out int index)) return false;
            if (arr.Length != FieldCount) return false;

            value = arr[index];
            return true;
        }

        /// <summary>
        /// Tries to read and cast a field value to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Target value type.</typeparam>
        /// <param name="record">Record array candidate.</param>
        /// <param name="fieldName">Field name.</param>
        /// <param name="value">Typed value on success; default value of <typeparamref name="T"/> otherwise.</param>
        /// <returns><see langword="true"/> when value exists and can be cast; otherwise <see langword="false"/>.</returns>
        public bool TryGet<T>(object record, string fieldName, out T value)
        {
            value = default!;
            if (!TryGet(record, fieldName, out object? raw)) return false;
            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            return false;
        }
    }
}
