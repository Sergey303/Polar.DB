using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public static class ArtifactSizer
{
    public static IReadOnlyList<ArtifactInfo> Collect(string directory, string rolePrefix)
    {
        if (!Directory.Exists(directory)) return Array.Empty<ArtifactInfo>();

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var file = new FileInfo(path);
                return new ArtifactInfo(InferRole(file.Name, rolePrefix), path, file.Length);
            })
            .OrderBy(static x => x.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string InferRole(string fileName, string rolePrefix)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower is "polar.db" or "data" || lower.EndsWith(".db") || lower.EndsWith(".sqlite"))
        {
            return rolePrefix + ":primary-db";
        }

        if (lower.EndsWith("-wal")) return rolePrefix + ":wal";
        if (lower.EndsWith("-shm")) return rolePrefix + ":shared-memory";
        if (lower is "state" || lower.EndsWith(".state")) return rolePrefix + ":state";
        if (lower.EndsWith(".index") || lower.Contains("index")) return rolePrefix + ":secondary-index";
        if (lower.Contains("checkpoint")) return rolePrefix + ":checkpoint";
        return rolePrefix + ":artifact";
    }
}
