using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.Services;

public static class EngineRuntimeResolver
{
    private const string PolarEngine = "polar-db";
    private const string SyntheticEngine = "synthetic";

    public static (string Engine, EngineRuntimeDescriptor Runtime) Resolve(string? cliEngine, ExperimentSpec spec)
    {
        var specEngine = Normalize(spec.Engine);
        var cli = Normalize(cliEngine);
        var nuget = NormalizeNuget(spec.Nuget);

        if (!string.IsNullOrWhiteSpace(cli) &&
            !string.IsNullOrWhiteSpace(specEngine) &&
            !cli.Equals(specEngine, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Engine mismatch: --engine='{cli}' but spec.engine='{specEngine}'.");
        }

        var engine = cli ?? specEngine;
        if (string.IsNullOrWhiteSpace(engine))
        {
            throw new InvalidOperationException("Engine is not specified. Set spec.engine or pass --engine.");
        }

        var runtime = ResolveRuntime(engine, nuget);
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

    private static string? NormalizeNuget(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
