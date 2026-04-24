using System;
using System.Diagnostics;

namespace Polar.DB.Bench.Exec;

public sealed class ExperimentRunner
{
    private readonly TemporaryBenchmarkFileCleaner _cleaner;

    public ExperimentRunner(TemporaryBenchmarkFileCleaner? cleaner = null)
    {
        _cleaner = cleaner ?? new TemporaryBenchmarkFileCleaner();
    }

    public ExperimentRunResult Run(
        RunPaths paths,
        string experimentId,
        string engineKey,
        Action<RunPaths> executeExperiment)
    {
        if (paths is null)
            throw new ArgumentNullException(nameof(paths));

        if (executeExperiment is null)
            throw new ArgumentNullException(nameof(executeExperiment));

        paths.EnsureCreated();

        var started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        ExperimentRunResult result;

        try
        {
            executeExperiment(paths);

            stopwatch.Stop();

            result = new ExperimentRunResult
            {
                RunId = paths.RunId,
                ExperimentId = experimentId,
                EngineKey = engineKey,
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Success = true,
                Artifacts = ArtifactScanner.ScanEngineArtifacts(paths)
            };

            result.Metrics["elapsedMs"] = stopwatch.Elapsed.TotalMilliseconds;
            result.Metrics["artifactTotalBytes"] = SumArtifactBytes(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            result = new ExperimentRunResult
            {
                RunId = paths.RunId,
                ExperimentId = experimentId,
                EngineKey = engineKey,
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Success = false,
                ErrorType = ex.GetType().FullName,
                ErrorMessage = ex.Message,
                Artifacts = ArtifactScanner.ScanEngineArtifacts(paths)
            };

            result.Metrics["elapsedMs"] = stopwatch.Elapsed.TotalMilliseconds;
            result.Metrics["artifactTotalBytes"] = SumArtifactBytes(result);
        }

        // Важно: raw result пишем ДО удаления временных файлов.
        // Так мы сохраняем размеры db/wal/shm/state/index/polar.db,
        // а потом уже чистим рабочую папку.
        RawResultWriter.Write(paths, result);

        var cleanup = _cleaner.CleanRunTemporaryFiles(paths);

        result = CopyWithCleanup(result, cleanup);

        // Перезаписываем raw result уже с отчетом об очистке.
        // Если хочется абсолютной неизменяемости raw после первого write,
        // эту вторую запись можно убрать, а cleanup писать отдельным *.cleanup.json.
        RawResultWriter.Write(paths, result);

        return result;
    }

    private static long SumArtifactBytes(ExperimentRunResult result)
    {
        long total = 0;

        foreach (var artifact in result.Artifacts)
            total += artifact.Bytes;

        return total;
    }

    private static ExperimentRunResult CopyWithCleanup(
        ExperimentRunResult source,
        CleanupReport cleanup)
    {
        return new ExperimentRunResult
        {
            RunId = source.RunId,
            ExperimentId = source.ExperimentId,
            EngineKey = source.EngineKey,
            StartedAtUtc = source.StartedAtUtc,
            FinishedAtUtc = source.FinishedAtUtc,
            Success = source.Success,
            ErrorType = source.ErrorType,
            ErrorMessage = source.ErrorMessage,
            Metrics = source.Metrics,
            Artifacts = source.Artifacts,
            Cleanup = cleanup
        };
    }
}