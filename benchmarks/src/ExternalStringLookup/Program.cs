using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;

// TODO: Replace PolarDbTodo() with real Polar.DB scenario code.
// Standalone file: no old benchmark runner/charts/core dependency.

const string ExperimentId = "external-string-lookup";
const string ExperimentTitle = "Equal-range lookup by non-unique string external key.";
const int SetupRows = 50_000;
const int MeasuredOps = 2_000;
const int WarmupOps = 300;

var repo = FindRepoRoot();
var work = Path.Combine(repo, "benchmarks", "work", ExperimentId);
var results = Path.Combine(repo, "benchmarks", "results");
if (Directory.Exists(work)) Directory.Delete(work, true);
Directory.CreateDirectory(work);
Directory.CreateDirectory(results);

var data = Dataset(SetupRows).ToArray();
var samples = new List<Sample>
{
    RunSqlite(data, Path.Combine(work, "sqlite")),
    PolarDbTodo(data, Path.Combine(work, "polar"))
};

var output = Path.Combine(results, ExperimentId + ".html");
File.WriteAllText(output, RenderHtml(samples), Encoding.UTF8);
Console.WriteLine(output);

Sample RunSqlite(Row[] data, string dir)
{
    Directory.CreateDirectory(dir);
    var db = Path.Combine(dir, "data.sqlite");
    using var c = new SqliteConnection($"Data Source={db}");
    c.Open();
    Exec(c, "PRAGMA journal_mode=WAL;");
    Exec(c, "CREATE TABLE rows(id INTEGER PRIMARY KEY, skey TEXT NOT NULL, external_id INTEGER NOT NULL, external_key TEXT NOT NULL, payload TEXT NOT NULL);");
    using (var tx = c.BeginTransaction())
    {
        using var ins = c.CreateCommand();
        ins.CommandText = "INSERT INTO rows(id,skey,external_id,external_key,payload) VALUES($id,$skey,$eid,$ekey,$payload)";
        var id = ins.Parameters.Add("$id", SqliteType.Integer);
        var skey = ins.Parameters.Add("$skey", SqliteType.Text);
        var eid = ins.Parameters.Add("$eid", SqliteType.Integer);
        var ekey = ins.Parameters.Add("$ekey", SqliteType.Text);
        var payload = ins.Parameters.Add("$payload", SqliteType.Text);
        foreach (var r in data)
        {
            id.Value = r.Id; skey.Value = r.SKey; eid.Value = r.ExternalId;
            ekey.Value = r.ExternalKey; payload.Value = r.Payload;
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }

    BuildSqliteIndexes(c);
    var keys = LookupKeys(data).ToArray();
    for (var i = 0; i < WarmupOps; i++) QuerySqlite(c, keys[i % keys.Length]);

    var sw = Stopwatch.StartNew();
    ulong checksum = 0;
    long rows = 0;
    for (var i = 0; i < MeasuredOps; i++)
    {
        var q = QuerySqlite(c, keys[i % keys.Length]);
        checksum ^= q.Checksum;
        rows += q.Rows;
    }
    sw.Stop();
    return new Sample("sqlite", sw.Elapsed.TotalMilliseconds, rows, checksum, "Measured", DirBytes(dir));
}

Sample PolarDbTodo(Row[] data, string dir)
{
    Directory.CreateDirectory(dir);
    // TODO create string external index; measure equal-range lookup and materialize all rows.
    // Fairness rules: exclude setup from named operation, materialize returned
    // values before checksum, use same keys as SQLite, and return NotSupported
    // for unsupported operations instead of emulating them by rebuild.
    return new Sample("polar-db-current", 0, 0, 0, "TODO: implement Polar.DB path", DirBytes(dir));
}

void BuildSqliteIndexes(SqliteConnection c)
{
    if (ExperimentId == "pk-string-lookup") Exec(c, "CREATE UNIQUE INDEX ix_rows_skey ON rows(skey);");
    if (ExperimentId == "external-int-lookup") Exec(c, "CREATE INDEX ix_rows_external_id ON rows(external_id);");
    if (ExperimentId == "external-string-lookup") Exec(c, "CREATE INDEX ix_rows_external_key ON rows(external_key);");
}

QueryResult QuerySqlite(SqliteConnection c, object key)
{
    using var cmd = c.CreateCommand();
    if (ExperimentId == "pk-int-lookup") { cmd.CommandText = "SELECT id,skey,external_id,external_key,payload FROM rows WHERE id=$v"; cmd.Parameters.AddWithValue("$v", (long)key); }
    else if (ExperimentId == "pk-string-lookup") { cmd.CommandText = "SELECT id,skey,external_id,external_key,payload FROM rows WHERE skey=$v"; cmd.Parameters.AddWithValue("$v", (string)key); }
    else if (ExperimentId == "external-int-lookup") { cmd.CommandText = "SELECT id,skey,external_id,external_key,payload FROM rows WHERE external_id=$v"; cmd.Parameters.AddWithValue("$v", (int)key); }
    else if (ExperimentId == "external-string-lookup") { cmd.CommandText = "SELECT id,skey,external_id,external_key,payload FROM rows WHERE external_key=$v"; cmd.Parameters.AddWithValue("$v", (string)key); }
    else return new QueryResult(0, 0);

    using var r = cmd.ExecuteReader();
    ulong checksum = 0;
    long rows = 0;
    while (r.Read())
    {
        var row = new Row(r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4));
        checksum ^= Hash(row);
        rows++;
    }
    return new QueryResult(rows, checksum);
}

IEnumerable<object> LookupKeys(Row[] data)
{
    foreach (var r in data.Take(MeasuredOps))
    {
        if (ExperimentId == "pk-int-lookup") yield return r.Id;
        else if (ExperimentId == "pk-string-lookup") yield return r.SKey;
        else if (ExperimentId == "external-int-lookup") yield return r.ExternalId;
        else if (ExperimentId == "external-string-lookup") yield return r.ExternalKey;
        else yield return r.Id;
    }
}

static IEnumerable<Row> Dataset(int count)
{
    for (var i = 1; i <= count; i++)
        yield return new Row(i, $"id-{i:000000000}", i % 1000, $"group-{i % 1000:0000}", $"payload-{i:000000000}");
}

static ulong Hash(Row r)
{
    unchecked
    {
        ulong h = 14695981039346656037UL;
        void Add(string s) { foreach (var ch in s) { h ^= ch; h *= 1099511628211UL; } }
        h ^= (ulong)r.Id; h *= 1099511628211UL;
        h ^= (ulong)r.ExternalId; h *= 1099511628211UL;
        Add(r.SKey); Add(r.ExternalKey); Add(r.Payload);
        return h;
    }
}

static void Exec(SqliteConnection c, string sql)
{
    using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
}

static long DirBytes(string dir) => Directory.Exists(dir)
    ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
    : 0;

static string FindRepoRoot()
{
    var d = new DirectoryInfo(Environment.CurrentDirectory);
    while (d != null)
    {
        if (File.Exists(Path.Combine(d.FullName, "src", "Polar.DB", "Polar.DB.csproj"))) return d.FullName;
        d = d.Parent;
    }
    return Environment.CurrentDirectory;
}

string RenderHtml(List<Sample> xs)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>" + ExperimentId + "</title>");
    sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:32px}table{border-collapse:collapse}td,th{border:1px solid #ddd;padding:6px 10px}th{background:#f4f4f4}.todo{color:#9a6700}</style></head><body>");
    sb.AppendLine("<h1>" + ExperimentTitle + "</h1>");
    sb.AppendLine("<p><b>Experiment:</b> " + ExperimentId + "</p>");
    sb.AppendLine("<p><b>Measured operation:</b> TODO verify final operation boundary before accepting numbers.</p>");
    sb.AppendLine("<p><b>Setup rows:</b> " + SetupRows + ", <b>warmup ops:</b> " + WarmupOps + ", <b>measured ops:</b> " + MeasuredOps + "</p>");
    sb.AppendLine("<h2>Timing</h2><table><tr><th>Engine</th><th>Status</th><th>Total ms</th><th>Rows materialized</th><th>Checksum</th><th>Artifact bytes</th></tr>");
    foreach (var x in xs)
        sb.AppendLine($"<tr><td>{x.Engine}</td><td>{x.Status}</td><td>{x.TotalMs:0.###}</td><td>{x.Rows}</td><td>{x.Checksum}</td><td>{x.Bytes}</td></tr>");
    sb.AppendLine("</table><h2>TODO</h2><p class=\"todo\">Implement exact Polar.DB API calls and split total timing into min/median/p95/trimmed mean after first local compile.</p>");
    sb.AppendLine("</body></html>");
    return sb.ToString();
}

record Row(long Id, string SKey, int ExternalId, string ExternalKey, string Payload);
record QueryResult(long Rows, ulong Checksum);
record Sample(string Engine, double TotalMs, long Rows, ulong Checksum, string Status, long Bytes);
