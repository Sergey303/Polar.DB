using System.Security.Cryptography;
using System.Text.Json;

namespace PolarDbBenchmarks;

internal static partial class PreparedLookupRunner
{
    private sealed record UniformSeal(int ProtocolVersion, string Protocol, int Rows,
        string Experiment, string Kind, string Engine, string DatasetSourceSha,
        string RunnerSourceSha, string DatasetManifestSha256, string KeyStreamSha256,
        ulong Seed, UniformLookupPlanner.Coverage Coverage,
        long BatchRows, ulong BatchChecksum, long LatencyRows, ulong LatencyChecksum);

    private static bool TryUniform(ExperimentOptions options, string[] args)
    {
        var protocol = Value(args, "prepared-plan");
        if (protocol is null) return false;
        if (protocol is not ("uniform-v3" or "uniform-v32"))
            throw new ArgumentException("Unknown prepared-plan.");
        var controlledCache = protocol == "uniform-v32";
        if (options.Kind != ExperimentKind.PkIntLookup)
            throw new NotSupportedException("Uniform v3 supports only PkIntLookup.");
        var stage = Value(args, "prepared-stage");
        if (stage is not ("plan" or "query" or "verify"))
            throw new ArgumentException("Uniform v3 requires plan, query, or verify stage.");
        var engine = BenchmarkArgs.Engine(args)
            ?? throw new ArgumentException("Uniform v3 requires --benchmark-engine.");
        var counts = BenchmarkArgs.Rows(args, options.RowCounts.ToArray());
        if (counts.Count != 1) throw new ArgumentException("Specify one --rows=N.");
        var rows = counts[0];
        var root = RequiredDirectory(args, "prepared-root");
        if (IsWithin(root, BenchmarkPaths.RepoRoot))
            throw new ArgumentException("Prepared data must reside outside repository.");
        var engineName = BenchmarkPaths.EngineToken(engine);
        var datasetDir = Path.Combine(root, options.ExperimentId,
            "rows-" + rows, engineName);
        var manifest = Path.Combine(root, options.ExperimentId,
            "rows-" + rows, engineName + ".ready.json");
        // A published query runner can use an older sealed dataset only if its
        // exact dataset SHA is explicitly provided, verified, and recorded.
        var expectedDatasetSha = Value(args, "prepared-source-sha")
            ?? throw new ArgumentException("Uniform v3 requires --prepared-source-sha.");
        if (expectedDatasetSha.Length != 40 || !expectedDatasetSha.All(Uri.IsHexDigit))
            throw new ArgumentException("Dataset SHA must be exactly 40 hex digits.");
        var currentSha = CurrentCommit();
        var seal = ReadSeal(manifest, options, engineName, rows, expectedDatasetSha);
        CheckFilesExist(datasetDir, seal);
        var sidecar = Path.Combine(root, options.ExperimentId,
            "rows-" + rows, engineName + "." + protocol + ".json");
        if (stage == "verify")
        {
            VerifyFiles(datasetDir, seal);
            if (File.Exists(sidecar)) ReadUniformSeal(sidecar, manifest, options,
                engineName, rows, expectedDatasetSha, currentSha,
                controlledCache ? 32 : 3);
        }
        else if (stage == "plan")
        {
            // Hash the data BEFORE creating the plan; no DB bytes are modified.
            VerifyFiles(datasetDir, seal);
            var (plan, coverage) = UniformLookupPlanner.Create(rows);
            var expected = UniformLookupPlanner.Expected(plan, options.Kind);
            var newPlan = new UniformSeal(controlledCache ? 32 : 3,
                UniformLookupPlanner.Protocol, rows,
                options.ExperimentId, options.Kind.ToString(), engineName,
                expectedDatasetSha, currentSha, HashFile(manifest),
                coverage.KeyStreamSha256, UniformLookupPlanner.Seed, coverage,
                expected.Batch.Rows, expected.Batch.Checksum,
                expected.Latency.Rows, expected.Latency.Checksum);
            WriteNewJson(sidecar, newPlan);
            Console.WriteLine($"[{protocol}] sealed plan, {coverage.UniqueKeys} unique keys, " +
                $"50k bins: {coverage.BinsTouched}/{coverage.BinsTotal}: {sidecar}");
        }
        else
        {
            var v3 = ReadUniformSeal(sidecar, manifest, options,
                engineName, rows, expectedDatasetSha, currentSha,
                controlledCache ? 32 : 3);
            var runId = BenchmarkArgs.RunId(args)
                ?? throw new ArgumentException("Uniform query requires --benchmark-run-id.");
            if (runId.Length is < 1 or > 100 || runId.Any(c =>
                    !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
                throw new ArgumentException("Invalid query run id.");
            var resultRoot = RequiredDirectory(args, "prepared-results-root");
            if (IsWithin(resultRoot, root) || IsWithin(root, resultRoot) ||
                IsWithin(resultRoot, BenchmarkPaths.RepoRoot))
                throw new ArgumentException("Result root must be separate from prepared files and repo.");
            var condition = Value(args, "prepared-cache-treatment");
            if (controlledCache)
            {
                if (condition is null)
                    throw new ArgumentException("v32 requires --prepared-cache-treatment.");
                PreparedCacheTreatment.Validate(condition, engineName);
            }
            else if (condition is not null)
                throw new ArgumentException("Cache treatments require uniform-v32.");
            Query(options, engine, rows, datasetDir, manifest, seal, runId,
                resultRoot, "hot", v3, condition);
        }
        return true;
    }

    private static string HashFile(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    private static UniformSeal ReadUniformSeal(string sidecar, string datasetManifest,
        ExperimentOptions options, string engine, int rows,
        string sourceSha, string runnerSha, int protocolVersion)
    {
        var record = JsonSerializer.Deserialize<UniformSeal>(File.ReadAllText(sidecar))
            ?? throw new InvalidDataException("Empty uniform plan.");
        var (plan, coverage) = UniformLookupPlanner.Create(rows);
        var expected = UniformLookupPlanner.Expected(plan, options.Kind);
        if (record.ProtocolVersion != protocolVersion ||
            record.Protocol != UniformLookupPlanner.Protocol ||
            record.Rows != rows || record.Experiment != options.ExperimentId ||
            record.Kind != options.Kind.ToString() || record.Engine != engine ||
            record.DatasetSourceSha != sourceSha || record.RunnerSourceSha != runnerSha ||
            record.DatasetManifestSha256 != HashFile(datasetManifest) ||
            record.Seed != UniformLookupPlanner.Seed || record.Coverage != coverage ||
            record.KeyStreamSha256 != coverage.KeyStreamSha256 ||
            record.BatchRows != expected.Batch.Rows ||
            record.BatchChecksum != expected.Batch.Checksum ||
            record.LatencyRows != expected.Latency.Rows ||
            record.LatencyChecksum != expected.Latency.Checksum)
            throw new InvalidDataException("Uniform plan provenance, key stream or checksum mismatch.");
        return record;
    }
}
