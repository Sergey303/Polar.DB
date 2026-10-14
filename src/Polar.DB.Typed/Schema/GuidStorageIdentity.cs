using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Polar.DB.Typed.Schema;

// A schema snapshot describes an intended layout, not proof of how a pre-existing
// data file was written. This marker binds the accepted physical layout to the
// table independently of schema.json, without changing the record byte format.
internal static class GuidStorageIdentity
{
    internal const string FileName = "guid-layout.v1";

    internal static void Ensure(string tablePath, string schemaPath,
        SchemeSnapshot snapshot, PType recordType, IReadOnlyList<FieldScheme> fields)
    {
        string markerPath = Path.Combine(tablePath, FileName);
        bool hasGuid = fields.Any(field => field.ClrType == typeof(Guid));
        if (!hasGuid && !File.Exists(markerPath))
            return;

        string layout = JsonSerializer.Serialize(snapshot);
        string fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(layout)));
        string expected = "PDB-GUID-LAYOUT-V1:" + fingerprint;

        if (File.Exists(markerPath))
        {
            if (!string.Equals(File.ReadAllText(markerPath).Trim(), expected,
                    StringComparison.Ordinal))
                throw new SchemeCompatibilityException(tablePath, schemaPath,
                    "Persistent Guid layout marker does not match the requested record layout.");
            return;
        }

        // Legacy and early native tables have no marker. Verify the physical
        // rows once, BEFORE accepting the layout; subsequent opens are O(1).
        string dataPath = Path.Combine(tablePath, "data-00.bin");
        if (File.Exists(dataPath) && !recordType.HasNoTail)
        {
            long length = new FileInfo(dataPath).Length;
            if (length > 0 && length < sizeof(long))
                throw new SchemeCompatibilityException(tablePath, schemaPath,
                    "Existing Guid data file has a truncated sequence header.");
            if (length >= sizeof(long))
                GuidStorageRowValidator.Validate(dataPath, recordType, fields,
                    tablePath, schemaPath);
        }

        SchemaJsonFile.WriteAtomic(markerPath, expected + "\n");
    }
}
