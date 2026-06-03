using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Polar.DB;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal static class LookupEngines
{
    public static EngineResult RunSqlite(LookupOptions o, Row[] data, string dir)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        CreateSqlite(db, o.Kind, data);

        using var c = new SqliteConnection($"Data Source={db}");
        c.Open();

        var keys = LookupBench.LookupKeys(data, o.Kind, o.MeasuredOps).ToArray();
        for (var i = 0; i < o.WarmupOps; i++) QuerySqlite(c, o.Kind, keys[i % keys.Length]);

        var samples = new List<double>();
        ulong checksum = 0;
        long rows = 0;
        foreach (var key in keys)
        {
            var sw = Stopwatch.StartNew();
            var q = QuerySqlite(c, o.Kind, key);
            sw.Stop();

            samples.Add(sw.Elapsed.TotalMilliseconds);
            checksum ^= q.Checksum;
            rows += q.Rows;
        }

        return new EngineResult("sqlite", "Measured", samples, rows, checksum, LookupBench.DirBytes(dir));
    }

    public static EngineResult RunPolarDb(LookupOptions o, Row[] data, string dir)
    {
        Directory.CreateDirectory(dir);

        var created = OpenPolar(dir, o.Kind);
        created.Sequence.Load(data.Select(ToPolar));
        created.Sequence.Build();
        created.Sequence.Flush();
        created.Sequence.Close();

        var store = OpenPolar(dir, o.Kind);
        store.Sequence.Refresh();

        var keys = LookupBench.LookupKeys(data, o.Kind, o.MeasuredOps).ToArray();
        for (var i = 0; i < o.WarmupOps; i++) QueryPolar(store, o.Kind, keys[i % keys.Length]);

        var samples = new List<double>();
        ulong checksum = 0;
        long rows = 0;
        foreach (var key in keys)
        {
            var sw = Stopwatch.StartNew();
            var q = QueryPolar(store, o.Kind, key);
            sw.Stop();

            samples.Add(sw.Elapsed.TotalMilliseconds);
            checksum ^= q.Checksum;
            rows += q.Rows;
        }

        store.Sequence.Close();
        return new EngineResult(
            "polar-db-current",
            "Measured",
            samples,
            rows,
            checksum,
            LookupBench.DirBytes(dir));
    }

    private static void CreateSqlite(string db, LookupKind kind, Row[] data)
    {
        using var c = new SqliteConnection($"Data Source={db}");
        c.Open();
        Exec(c, "PRAGMA journal_mode=WAL;");
        Exec(c, "CREATE TABLE rows(id INTEGER PRIMARY KEY, skey TEXT NOT NULL, external_id INTEGER NOT NULL, external_key TEXT NOT NULL, payload TEXT NOT NULL);");

        using (var tx = c.BeginTransaction())
        using (var ins = c.CreateCommand())
        {
            ins.CommandText = "INSERT INTO rows(id,skey,external_id,external_key,payload) VALUES($id,$skey,$eid,$ekey,$payload)";
            var id = ins.Parameters.Add("$id", SqliteType.Integer);
            var skey = ins.Parameters.Add("$skey", SqliteType.Text);
            var eid = ins.Parameters.Add("$eid", SqliteType.Integer);
            var ekey = ins.Parameters.Add("$ekey", SqliteType.Text);
            var payload = ins.Parameters.Add("$payload", SqliteType.Text);

            foreach (var r in data)
            {
                id.Value = r.Id;
                skey.Value = r.SKey;
                eid.Value = r.ExternalId;
                ekey.Value = r.ExternalKey;
                payload.Value = r.Payload;
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }

        if (kind == LookupKind.PrimaryString) Exec(c, "CREATE UNIQUE INDEX ix_rows_skey ON rows(skey);");
        if (kind == LookupKind.ExternalInt) Exec(c, "CREATE INDEX ix_rows_external_id ON rows(external_id);");
        if (kind == LookupKind.ExternalString) Exec(c, "CREATE INDEX ix_rows_external_key ON rows(external_key);");
    }

    private static QueryResult QuerySqlite(SqliteConnection c, LookupKind kind, object key)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = kind switch
        {
            LookupKind.PrimaryInt => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE id=$v",
            LookupKind.PrimaryString => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE skey=$v",
            LookupKind.ExternalInt => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE external_id=$v",
            _ => "SELECT id,skey,external_id,external_key,payload FROM rows WHERE external_key=$v"
        };
        cmd.Parameters.AddWithValue("$v", key);

        using var r = cmd.ExecuteReader();
        ulong checksum = 0;
        long rows = 0;
        while (r.Read())
        {
            checksum ^= LookupBench.Hash(new Row(r.GetInt64(0), r.GetString(1),
                r.GetInt32(2), r.GetString(3), r.GetString(4)));
            rows++;
        }

        return new QueryResult(rows, checksum);
    }

private static QueryResult QueryPolar(PolarStore store, LookupKind kind, object key)
    {
        IEnumerable<object> values = kind switch
        {
            LookupKind.PrimaryInt or LookupKind.PrimaryString => One(store.Sequence.GetByKey((IComparable)key)),
            LookupKind.ExternalInt or LookupKind.ExternalString => store.ExternalIndex!.GetManyByKey((IComparable)key),
            _ => Array.Empty<object>()
        };

        ulong checksum = 0;
        long rows = 0;
        foreach (var value in values)
        {
            checksum ^= LookupBench.Hash(FromPolar(value));
            rows++;
        }

        return new QueryResult(rows, checksum);
    }

    private static PolarStore OpenPolar(string dir, LookupKind kind)
    {
        var counter = 0;
        Stream StreamGen()
        {
            var path = Path.Combine(dir, "f" + counter++ + ".bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        var type = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.longinteger)),
            new NamedType("skey", new PType(PTypeEnumeration.sstring)),
            new NamedType("external_id", new PType(PTypeEnumeration.integer)),
            new NamedType("external_key", new PType(PTypeEnumeration.sstring)),
            new NamedType("payload", new PType(PTypeEnumeration.sstring)),
            new NamedType("deleted", new PType(PTypeEnumeration.boolean)));

        Func<object, bool> isEmpty = o => (bool)((object[])o)[5];
        Func<object, IComparable> primary = kind == LookupKind.PrimaryString
            ? o => (string)((object[])o)[1]
            : o => (long)((object[])o)[0];

        var sequence = new USequence(
            type,
            Path.Combine(dir, "state.bin"),
            StreamGen,
            isEmpty,
            primary,
            StableHash);

        EKeyIndex? external = null;
        if (kind is LookupKind.ExternalInt or LookupKind.ExternalString)
        {
            Func<object, IEnumerable<IComparable>> keys = kind == LookupKind.ExternalInt
                ? o => new IComparable[] { (int)((object[])o)[2] }
                : o => new IComparable[] { (string)((object[])o)[3] };

            external = new EKeyIndex(StreamGen, sequence, keys, StableHash);
            sequence.uindexes = new IUIndex[] { external };
        }

        return new PolarStore(sequence, external);
    }

    private static IEnumerable<object> One(object? value)
    {
        if (value != null) yield return value;
    }

    private static object ToPolar(Row r) => new object[]
    {
        r.Id,
        r.SKey,
        r.ExternalId,
        r.ExternalKey,
        r.Payload,
        false
    };

    private static Row FromPolar(object value)
    {
        var r = (object[])value;
        return new Row((long)r[0], (string)r[1], (int)r[2], (string)r[3], (string)r[4]);
    }

    private static int StableHash(IComparable key)
    {
        unchecked
        {
            return key switch
            {
                int i => i,
                long l => (int)(l ^ (l >> 32)),
                string s => StableStringHash(s),
                _ => key.GetHashCode()
            };
        }
    }

    private static int StableStringHash(string value)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in value)
            {
                h ^= c;
                h *= 16777619;
            }

            return h;
        }
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private sealed record PolarStore(USequence Sequence, EKeyIndex? ExternalIndex);
}
