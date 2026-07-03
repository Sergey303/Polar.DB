namespace Polar.DB.Typed;

public sealed class SchemeBuildException : Exception
{
    public SchemeBuildException(
        Type recordType,
        string? fieldName,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RecordType = recordType ?? throw new ArgumentNullException(nameof(recordType));
        FieldName = fieldName;
    }

    public Type RecordType { get; }
    public string? FieldName { get; }
}
