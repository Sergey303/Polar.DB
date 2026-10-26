using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static partial class SqliteStore
{
    private enum IndexProfile
    {
        None, LegacyFull, PrimaryId, PrimaryLong, PrimaryGuid, PrimaryString,
        ExternalInt, ExternalLong, ExternalGuid, ExternalString, MutationAndReopen
    }

    // One logical primary key + the requested secondary index, without unused indexes.
    public static void CreateForLookup(string db, Row[] rows, ExperimentKind kind) =>
        Create(db, rows, kind switch
        {
            ExperimentKind.PkIntLookup => IndexProfile.PrimaryId,
            ExperimentKind.PkLongLookup => IndexProfile.PrimaryLong,
            ExperimentKind.PkGuidLookup => IndexProfile.PrimaryGuid,
            ExperimentKind.PkStringLookup => IndexProfile.PrimaryString,
            ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup => IndexProfile.ExternalInt,
            ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup => IndexProfile.ExternalLong,
            ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup => IndexProfile.ExternalGuid,
            ExperimentKind.ExternalStringLookup or ExperimentKind.ExternalFamousStringLookup => IndexProfile.ExternalString,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a lookup experiment")
        });

    // Both engines maintain id and exactly four non-unique external indexes.
    public static void CreateForMutation(string db, Row[] rows) =>
        Create(db, rows, IndexProfile.MutationAndReopen);

    public static void CreateForReopen(string db, Row[] rows) =>
        Create(db, rows, IndexProfile.MutationAndReopen);

    private static void Create(string db, Row[] rows, IndexProfile profile)
    {
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        Exec(connection, "PRAGMA journal_mode=WAL;");
        var alternatePrimary = profile is IndexProfile.PrimaryLong
            or IndexProfile.PrimaryGuid or IndexProfile.PrimaryString;
        CreateTable(connection, alternatePrimary ? "id INTEGER NOT NULL" : "id INTEGER PRIMARY KEY");
        InsertRows(connection, rows);
        switch (profile)
        {
            case IndexProfile.LegacyFull:
                CreateIndexes(connection);
                break;
            case IndexProfile.PrimaryLong:
                Exec(connection, "CREATE UNIQUE INDEX ix_rows_long_key ON rows(long_key);");
                break;
            case IndexProfile.PrimaryGuid:
                Exec(connection, "CREATE UNIQUE INDEX ix_rows_guid_key ON rows(guid_key);");
                break;
            case IndexProfile.PrimaryString:
                Exec(connection, "CREATE UNIQUE INDEX ix_rows_skey ON rows(skey);");
                break;
            case IndexProfile.ExternalInt:
                Exec(connection, "CREATE INDEX ix_rows_external_id ON rows(external_id);");
                break;
            case IndexProfile.ExternalLong:
                Exec(connection, "CREATE INDEX ix_rows_external_long ON rows(external_long);");
                break;
            case IndexProfile.ExternalGuid:
                Exec(connection, "CREATE INDEX ix_rows_external_guid ON rows(external_guid);");
                break;
            case IndexProfile.ExternalString:
                Exec(connection, "CREATE INDEX ix_rows_external_key ON rows(external_key);");
                break;
            case IndexProfile.MutationAndReopen:
                CreateExternalIndexes(connection);
                break;
        }

        var expected = profile switch
        {
            IndexProfile.LegacyFull => new[] { "ix_rows_long_key", "ix_rows_guid_key", "ix_rows_skey",
                "ix_rows_external_id", "ix_rows_external_long", "ix_rows_external_guid", "ix_rows_external_key" },
            IndexProfile.PrimaryLong => new[] { "ix_rows_long_key" },
            IndexProfile.PrimaryGuid => new[] { "ix_rows_guid_key" },
            IndexProfile.PrimaryString => new[] { "ix_rows_skey" },
            IndexProfile.ExternalInt => new[] { "ix_rows_external_id" },
            IndexProfile.ExternalLong => new[] { "ix_rows_external_long" },
            IndexProfile.ExternalGuid => new[] { "ix_rows_external_guid" },
            IndexProfile.ExternalString => new[] { "ix_rows_external_key" },
            IndexProfile.MutationAndReopen => new[] { "ix_rows_external_id", "ix_rows_external_long",
                "ix_rows_external_guid", "ix_rows_external_key" },
            _ => Array.Empty<string>()
        };
        AssertIndexProfile(connection, expected);
    }

    private static void AssertIndexProfile(SqliteConnection connection, string[] expected)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA index_list('rows');";
        using var reader = command.ExecuteReader();
        var actual = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
            actual.Add(reader.GetString(1));
        if (!actual.SetEquals(expected))
            throw new InvalidDataException(
                $"SQLite index profile differs: actual={string.Join(",", actual)}; expected={string.Join(",", expected)}");
    }

}
