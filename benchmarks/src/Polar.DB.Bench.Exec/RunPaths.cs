using System;
using System.IO;

namespace Polar.DB.Bench.Exec;

public sealed class RunPaths
{
    public RunPaths(string benchmarksRoot, string runId)
    {
        if (string.IsNullOrWhiteSpace(benchmarksRoot))
            throw new ArgumentException("Benchmarks root path is required.", nameof(benchmarksRoot));

        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run id is required.", nameof(runId));

        BenchmarksRoot = Path.GetFullPath(benchmarksRoot);
        RunId = runId;

        ResultsRawDirectory = Path.Combine(BenchmarksRoot, "results", "raw");
        RunDirectory = Path.Combine(BenchmarksRoot, "runs", runId);
        WorkDirectory = Path.Combine(RunDirectory, "work");
        DataDirectory = Path.Combine(RunDirectory, "data");
        ArtifactsDirectory = Path.Combine(RunDirectory, "artifacts");

        RawResultPath = Path.Combine(ResultsRawDirectory, $"{runId}.run.json");
    }

    public string BenchmarksRoot { get; }

    public string RunId { get; }

    public string ResultsRawDirectory { get; }

    public string RunDirectory { get; }

    public string WorkDirectory { get; }

    public string DataDirectory { get; }

    public string ArtifactsDirectory { get; }

    public string RawResultPath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(ResultsRawDirectory);
        Directory.CreateDirectory(RunDirectory);
        Directory.CreateDirectory(WorkDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ArtifactsDirectory);
    }
}