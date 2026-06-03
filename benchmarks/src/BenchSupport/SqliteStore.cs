using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteStore
{
    public static void Create(string db, Row[] rows, bool withIndexes)
    {
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        Exec(connection, "PRAGMA journal_mode=WAL;");
        Exec(connection, "CREATE TABLE rows(id INTEGER PRIMARY KEY, long_key INTEGER NOT NULL, guid_key TEXT NOT NULL, skey TEXT NOT NULL, external_id INTEGER NOT NULL, external_key TEXT NOT NULL, payload TEXT NOT NULL);");
        InsertRows(connection, rows);
        if (withIndexes) CreateIndexes(connection);
    }

    public static void InsertRows(SqliteConnection connection, IEnumerable<Row> rows)
    {
        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO rows(id,long_key,guid_key,skey,external_id,external_key,payload) VALUES($id,$long,$guid,$skey,$eid,$ekey,$payload)";
        var id = command.Parameters.Add("$id", SqliteType.Integer);
        var longKey = command.Parameters.Add("$long", SqliteType.Integer);
        var guidKey = command.Parameters.Add("$guid", SqliteType.Text);
        var skey = command.Parameters.Add("$skey", SqliteType.Text);
        var externalId = command.Parameters.Add("$eid", SqliteType.Integer);
        var externalKey = command.Parameters.Add("$ekey", SqliteType.Text);
        var payload = command.Parameters.Add("$payload", SqliteType.Text);

        foreach (var row in rows)
        {
            id.Value = row.Id;
            longKey.Value = row.LongKey;
            guidKey.Value = row.GuidKey;
            skey.Value = row.SKey;
            externalId.Value = row.ExternalId;
            externalKey.Value = row.ExternalKey;
            payload.Value = row.Payload;
            command.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public static void CreateIndexes(SqliteConnection connection)
    {
        Exec(connection, "CREATE UNIQUE INDEX ix_rows_long_key ON rows(long_key);");
        Exec(connection, "CREATE UNIQUE INDEX ix_rows_guid_key ON rows(guid_key);");
        Exec(connection, "CREATE UNIQUE INDEX ix_rows_skey ON rows(skey);");
        Exec(connection, "CREATE INDEX ix_rows_external_id ON rows(external_id);");
        Exec(connection, "CREATE INDEX ix_rows_external_key ON rows(external_key);");
    }

    public static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
