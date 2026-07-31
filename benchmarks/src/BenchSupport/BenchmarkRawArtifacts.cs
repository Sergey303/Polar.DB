using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolarDbBenchmarks;

internal static class BenchmarkRawArtifacts
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void WriteWorker(BenchmarkWorkerResult result)
    {
        var path = BenchmarkPaths.WorkerResultPath(result.ExperimentId, result.RunId, result.Engine);
        WriteJson(path, result);
        Console.WriteLine("[bench] worker raw: " + path);
    }

    public static BenchmarkWorkerResult ReadWorker(string experimentId, string runId, BenchmarkEngine engine)
    {
        var path = BenchmarkPaths.WorkerResultPath(experimentId, runId, engine);
        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<BenchmarkWorkerResult>(json, JsonOptions)
            ?? throw new InvalidDataException("Worker result is empty: " + path);
    }

    public static void WriteCombined(
        ExperimentOptions options,
        BenchmarkRunManifest manifest,
        IReadOnlyList<BenchmarkRunResult>? lifecycleRuns,
        IReadOnlyList<LookupRunResult>? lookupRuns)
    {
        var raw = new BenchmarkCombinedRaw(options, manifest, lifecycleRuns, lookupRuns);
        WriteJson(BenchmarkPaths.ImmutableManifestPath(options.ExperimentId, manifest.RunId), manifest);
        WriteJson(BenchmarkPaths.ImmutableRawJsonPath(options.ExperimentId, manifest.RunId), raw);
        WriteJson(BenchmarkPaths.LatestManifestPath(options.ExperimentId), manifest);
        WriteJson(BenchmarkPaths.LatestRawJsonPath(options.ExperimentId), raw);

        var csv = RenderCsv(options, manifest, lifecycleRuns, lookupRuns);
        WriteText(BenchmarkPaths.ImmutableRawCsvPath(options.ExperimentId, manifest.RunId), csv);
        WriteText(BenchmarkPaths.LatestRawCsvPath(options.ExperimentId), csv);
    }

    private static string RenderCsv(
        ExperimentOptions options,
        BenchmarkRunManifest manifest,
        IReadOnlyList<BenchmarkRunResult>? lifecycleRuns,
        IReadOnlyList<LookupRunResult>? lookupRuns)
    {
        var builder = new StringBuilder();
        builder.AppendLine("run_id,experiment_id,kind,engine,setup_rows,phase,metric,sample_index,value_ms,batch_size");

        if (lifecycleRuns != null)
        {
            foreach (var run in lifecycleRuns)
            foreach (var engine in run.Engines)
            {
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", engine.Metric,
                    engine.SamplesMs, 1);
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "open-only",
                    engine.OpenSamplesMs, 1);
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "load",
                    engine.LoadSamplesMs, 1);
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build",
                    engine.BuildSamplesMs, 1);
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "flush",
                    engine.FlushSamplesMs, 1);
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "durable-per-operation",
                    engine.DurableSamplesMs, engine.DurableBatchSize);
                if (engine.PrimaryBuildStages != null)
                {
                    var stages = engine.PrimaryBuildStages;
                    AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build-scan", stages.ScanMs, 1);
                    AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build-to-arrays", stages.ToArrayMs, 1);
                    AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build-sort", stages.SortMs, 1);
                    AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build-write-hashes", stages.WriteHashKeysMs, 1);
                    AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build-write-offsets", stages.WriteOffsetsMs, 1);
                    AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build-gc", stages.GcMs, 1);
                    AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, "", "build-profile-total", stages.ProfileTotalMs, 1);
                }
            }
        }

        if (lookupRuns != null)
        {
            foreach (var run in lookupRuns)
            foreach (var phase in run.Phases)
            foreach (var engine in phase.Engines)
            {
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, phase.Name,
                    "lookup-batch-average", engine.BatchAvgSamplesMs, 1);
                AppendSamples(builder, manifest, options, engine.Engine, run.SetupRows, phase.Name,
                    "lookup-latency", engine.LatencySamplesMs, 1);
            }
        }

        return builder.ToString();
    }

    private static void AppendSamples(
        StringBuilder builder,
        BenchmarkRunManifest manifest,
        ExperimentOptions options,
        string engine,
        int setupRows,
        string phase,
        string metric,
        IReadOnlyList<double>? values,
        int batchSize)
    {
        if (values == null) return;
        for (var i = 0; i < values.Count; i++)
        {
            builder.Append(Csv(manifest.RunId)).Append(',')
                .Append(Csv(options.ExperimentId)).Append(',')
                .Append(Csv(options.Kind.ToString())).Append(',')
                .Append(Csv(engine)).Append(',')
                .Append(setupRows.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(phase)).Append(',')
                .Append(Csv(metric)).Append(',')
                .Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(values[i].ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(batchSize.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }
    }

    private static string Csv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void WriteJson<T>(string path, T value) =>
        WriteText(path, JsonSerializer.Serialize(value, JsonOptions));

    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
