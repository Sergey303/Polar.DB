using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteStore
{
    public static void Create(string db, Row[] rows, bool withIndexes)
    {
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        Exec(connection, "PRAGMA journal_mode=WAL;");
        CreateTable(connection, "id INTEGER PRIMARY KEY");
        InsertRows(connection, rows);
        if (withIndexes) CreateIndexes(connection);
    }

    public static void CreateForPrimaryIntBuild(string db, Row[] rows)
    {
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        Exec(connection, "PRAGMA journal_mode=WAL;");
        CreateTable(connection, "id INTEGER NOT NULL");
        InsertRows(connection, rows);
    }

    public static void InsertRows(SqliteConnection connection, IEnumerable<Row> rows)
    {
        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO rows(id,long_key,guid_key,skey,external_id,external_long,external_guid,external_key,payload) VALUES($id,$long,$guid,$skey,$eid,$elong,$eguid,$ekey,$payload)";
        var id = command.Parameters.Add("$id", SqliteType.Integer);
        var longKey = command.Parameters.Add("$long", SqliteType.Integer);
        var guidKey = command.Parameters.Add("$guid", SqliteType.Blob);
        var skey = command.Parameters.Add("$skey", SqliteType.Text);
        var externalId = command.Parameters.Add("$eid", SqliteType.Integer);
        var externalLong = command.Parameters.Add("$elong", SqliteType.Integer);
        var externalGuid = command.Parameters.Add("$eguid", SqliteType.Blob);
        var externalKey = command.Parameters.Add("$ekey", SqliteType.Text);
        var payload = command.Parameters.Add("$payload", SqliteType.Text);

        foreach (var row in rows)
        {
            id.Value = row.Id; longKey.Value = row.LongKey; guidKey.Value = BenchmarkGuid.ToBytes(row.GuidKey);
            skey.Value = row.SKey; externalId.Value = row.ExternalId; externalLong.Value = row.ExternalLong;
            externalGuid.Value = BenchmarkGuid.ToBytes(row.ExternalGuid); externalKey.Value = row.ExternalKey;
            payload.Value = row.Payload; command.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static void CreatePrimaryIntIndex(SqliteConnection connection) =>
        Exec(connection, "CREATE UNIQUE INDEX ix_rows_id ON rows(id);");

    public static void Flush(SqliteConnection connection) =>
        Exec(connection, "PRAGMA wal_checkpoint(TRUNCATE);");

    public static void CreateIndexes(SqliteConnection connection)
    {
        Exec(connection, "CREATE UNIQUE INDEX ix_rows_long_key ON rows(long_key);");
        Exec(connection, "CREATE UNIQUE INDEX ix_rows_guid_key ON rows(guid_key);");
        Exec(connection, "CREATE UNIQUE INDEX ix_rows_skey ON rows(skey);");
        Exec(connection, "CREATE INDEX ix_rows_external_id ON rows(external_id);");
        Exec(connection, "CREATE INDEX ix_rows_external_long ON rows(external_long);");
        Exec(connection, "CREATE INDEX ix_rows_external_guid ON rows(external_guid);");
        Exec(connection, "CREATE INDEX ix_rows_external_key ON rows(external_key);");
    }

    public static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void CreateTable(SqliteConnection connection, string idColumn)
    {
        Exec(connection, "CREATE TABLE rows(" + idColumn + ", long_key INTEGER NOT NULL, guid_key BLOB NOT NULL, skey TEXT NOT NULL, external_id INTEGER NOT NULL, external_long INTEGER NOT NULL, external_guid BLOB NOT NULL, external_key TEXT NOT NULL, payload TEXT NOT NULL);");
    }
}
