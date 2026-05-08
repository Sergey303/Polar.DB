using System;
using System.Collections.Generic;
using System.IO;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.PolarDb;

internal static partial class PolarDbStringLikeLookupExecutor
{
    private sealed record PolarDbStringLayout(
        string Root,
        string DataPath,
        string PrimaryHashIndexPath,
        string PrimaryOffsetIndexPath,
        string NameValueIndexPath,
        string NameOffsetIndexPath,
        string StatePath);

    private static PolarDbStringLayout CreateLayout(RunWorkspace workspace, string runId)
    {
        var root = Path.Combine(workspace.ArtifactsDirectory ?? workspace.WorkingDirectory, runId, "polar-db-string-like");
        return new PolarDbStringLayout(
            root,
            Path.Combine(root, "sequence.polar.db"),
            Path.Combine(root, "primary.hkeys.index"),
            Path.Combine(root, "primary.offsets.index"),
            Path.Combine(root, "name.values.index"),
            Path.Combine(root, "name.offsets.index"),
            Path.Combine(root, "sequence.state"));
    }

    private static IReadOnlyList<ArtifactDescriptor> CollectArtifacts(string root, string relativeRoot)
    {
        if (!Directory.Exists(root)) return Array.Empty<ArtifactDescriptor>();
        var result = new List<ArtifactDescriptor>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            result.Add(new ArtifactDescriptor(Role(info.Name), Path.GetRelativePath(relativeRoot, info.FullName), info.Length));
        }
        return result;
    }

    private static ArtifactRole Role(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("state", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.State;
        if (lower.Contains("index", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.SecondaryIndex;
        if (lower.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.PrimaryData;
        return ArtifactRole.Unknown;
    }
}
