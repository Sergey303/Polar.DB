using System.Security.Cryptography;
using System.Text.Json;

namespace PolarDbBenchmarks;

internal static partial class PreparedLookupRunner
{
    private static DatasetSeal ReadSeal(string path, ExperimentOptions options,
        string engine, int rows, string sha)
    {
        var seal = JsonSerializer.Deserialize<DatasetSeal>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Prepared manifest is empty.");
        if (seal.Schema != SchemaVersion || seal.Experiment != options.ExperimentId ||
            seal.Kind != options.Kind.ToString() || seal.Rows != rows ||
            seal.Engine != engine || seal.SourceSha != sha || seal.Files.Length == 0 ||
            seal.LookupsPerSample <= 0 || seal.ExpectedPhases is not { Length: 2 } ||
            seal.ExpectedPhases.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 2)
            throw new InvalidDataException("Prepared manifest does not match code, engine, kind or row count.");
        return seal;
    }

    // Read-only SQLite may create an empty WAL and a shared-memory file even
    // when no durable bytes change. They must not be sealed as database content.
    // A nonempty WAL, however, would mean the prepared state is not stable.
    private static bool IsTransientSqliteFile(string dir, string path, bool sqlite)
    {
        if (!sqlite) return false;
        var name = Path.GetRelativePath(dir, path);
        if (name == "data.sqlite-wal")
        {
            if (new FileInfo(path).Length != 0)
                throw new InvalidDataException("Prepared SQLite contains a nonempty WAL.");
            return true;
        }
        return name == "data.sqlite-shm";
    }

    private static void CheckFilesExist(string dir, DatasetSeal seal)
    {
        var sqlite = seal.Engine == "sqlite";
        var names = seal.Files.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var actual = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Where(p => !IsTransientSqliteFile(dir, p, sqlite))
            .Select(p => Path.GetRelativePath(dir, p)).ToHashSet(StringComparer.Ordinal);
        if (!names.SetEquals(actual)) throw new InvalidDataException("Prepared file set changed.");
        foreach (var f in seal.Files)
        {
            if (f.Name.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(f.Name) ||
                new FileInfo(Path.Combine(dir, f.Name)).Length != f.Bytes)
                throw new InvalidDataException("Prepared file size/path changed: " + f.Name);
        }
    }

    private static void VerifyFiles(string dir, DatasetSeal seal)
    {
        var now = SealedFiles(dir, seal.Engine == "sqlite");
        if (!seal.Files.SequenceEqual(now))
            throw new InvalidDataException("Prepared files changed since build: hash or size mismatch.");
        Console.WriteLine($"[prepared] verified {now.Length} file SHA256 hashes.");
    }

    private static FileSeal[] SealedFiles(string dir, bool sqlite) =>
        Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Where(p => !IsTransientSqliteFile(dir, p, sqlite))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => {
                using var fs = File.OpenRead(path);
                return new FileSeal(Path.GetRelativePath(dir, path), fs.Length,
                    Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant());
            }).ToArray();

    private static void WarmupSealedFiles(string dir, IReadOnlyList<FileSeal> files)
    {
        var buffer = new byte[1024 * 1024];
        foreach (var file in files)
        {
            using var stream = File.OpenRead(Path.Combine(dir, file.Name));
            while (stream.Read(buffer) != 0) { }
        }
    }

}
