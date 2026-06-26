namespace Polar.DB.Typed;

public sealed class SchemeCompatibilityException : InvalidOperationException
{
    public SchemeCompatibilityException(string tablePath, string schemaPath, string detail)
        : base($"Stored scheme is not compatible with the requested record type. {detail} Table: '{tablePath}'.")
    {
        TablePath = tablePath;
        SchemaPath = schemaPath;
        Detail = detail;
    }

    public string TablePath { get; }
    public string SchemaPath { get; }
    public string Detail { get; }
}
