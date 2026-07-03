using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteTinyPrimaryStore
{
    public static void Create(string db, IEnumerable<Row> rows)
    {
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        SqliteStore.Exec(connection, "PRAGMA journal_mode=WAL;");
        SqliteStore.Exec(connection, "CREATE TABLE rows(id INTEGER NOT NULL);");

        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO rows(id) VALUES($id)";
        var id = command.Parameters.Add("$id", SqliteType.Integer);

        foreach (var row in rows)
        {
            id.Value = row.Id;
            command.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
