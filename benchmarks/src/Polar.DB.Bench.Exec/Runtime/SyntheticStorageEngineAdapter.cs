using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Exec.Runtime;

internal sealed class SyntheticStorageEngineAdapter : IStorageEngineAdapter
{
    public string EngineKey => "synthetic";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.PhysicalArtifactInspection
    };

    public IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace)
    {
        return new SyntheticEngineRun(spec, workspace, EngineKey);
    }

    private sealed class SyntheticEngineRun : IEngineRun
    {
        private readonly ExperimentSpec _spec;
        private readonly RunWorkspace _workspace;
        private readonly string _engineKey;

        public SyntheticEngineRun(ExperimentSpec spec, RunWorkspace workspace, string engineKey)
        {
            _spec = spec;
            _workspace = workspace;
            _engineKey = engineKey;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<RunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var seed = _spec.Dataset.Seed ?? 0;
            var random = new Random(seed);
            var recordCount = _spec.Dataset.RecordCount;
            var lookupCount = _spec.Workload.LookupCount ?? 0;
            var elapsed = 800 + (recordCount / 200) + (lookupCount / 250) + random.Next(0, 10);
            var peakManaged = 32_000_000 + recordCount * 12;
            var totalBytes = 1_000_000 + recordCount * 8;

            var manifest = EnvironmentCollector.Collect(
                environmentClass: _workspace.EnvironmentClass,
                repositoryRoot: _workspace.RootDirectory);

            var artifactPath = Path.Combine(_workspace.ArtifactsDirectory!, "synthetic-primary.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            File.WriteAllText(artifactPath, "synthetic stage-1 artifact");

            var result = new RunResult
            {
                RunId = RunIdFactory.Create(_spec.ExperimentKey, _spec.Dataset.ProfileKey, _engineKey, manifest.EnvironmentClass),
                TimestampUtc = DateTimeOffset.UtcNow,
                EngineKey = _engineKey,
                ExperimentKey = _spec.ExperimentKey,
                DatasetProfileKey = _spec.Dataset.ProfileKey,
                FairnessProfileKey = _spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
                Environment = manifest,
                TechnicalSuccess = true,
                SemanticSuccess = true,
                Metrics = new[]
                {
                    new RunMetric { MetricKey = "elapsedMsSingleRun", Value = elapsed },
                    new RunMetric { MetricKey = "totalArtifactBytes", Value = totalBytes },
                    new RunMetric { MetricKey = "peakManagedBytes", Value = peakManaged }
                },
                Artifacts = new[]
                {
                    new ArtifactDescriptor(ArtifactRole.PrimaryData, "artifacts/synthetic-primary.bin", new FileInfo(artifactPath).Length, "Synthetic stage-1 artifact")
                },
                EngineDiagnostics = new Dictionary<string, string>
                {
                    ["mode"] = "synthetic",
                    ["note"] = "Used only to validate stage-1 raw/analyzed/charts flow."
                },
                Tags = new Dictionary<string, string>
                {
                    ["research"] = _spec.ResearchQuestionId ?? string.Empty,
                    ["hypothesis"] = _spec.HypothesisId ?? string.Empty
                },
                Notes = new List<string>
                {
                    "Synthetic stage-1 execution only.",
                    "Replace with real engine adapters in later stages."
                }
            };

            return Task.FromResult(result);
        }
    }
}
