using System;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.Services;

public static class EngineRuntimeResolver
{
    private const string PolarEngine = "polar-db";
    private const string SyntheticEngine = "synthetic";

    /// <summary>
    /// Resolves the engine family key and runtime descriptor from an experiment spec.
    /// The spec already contains the resolved target key, engine family, and optional nuget version.
    /// </summary>
    public static (string EngineKey, EngineRuntimeDescriptor Runtime) Resolve(ExperimentSpec spec)
    {
        var engine = Normalize(spec.Engine);
        if (string.IsNullOrWhiteSpace(engine))
        {
            throw new InvalidOperationException("Engine is not specified in experiment spec.");
        }

        var runtime = ResolveRuntime(engine, spec.Nuget);
        return (engine, runtime);
    }

    public static EngineRuntimeDescriptor ResolveRuntime(string engine, string? nuget)
    {
        if (!string.IsNullOrWhiteSpace(nuget))
        {
            return new EngineRuntimeDescriptor
            {
                Source = "nuget-pinned",
                Nuget = nuget
            };
        }

        if (engine.Equals(PolarEngine, StringComparison.OrdinalIgnoreCase) ||
            engine.Equals(SyntheticEngine, StringComparison.OrdinalIgnoreCase))
        {
            return new EngineRuntimeDescriptor
            {
                Source = "source-current"
            };
        }

        return new EngineRuntimeDescriptor
        {
            Source = "nuget-latest"
        };
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }
}
