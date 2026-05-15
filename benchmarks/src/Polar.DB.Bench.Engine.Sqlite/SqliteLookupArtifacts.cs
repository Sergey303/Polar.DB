using System.Collections.Generic;
using System.IO;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static class SqliteLookupArtifacts
{
    public static SqliteLookupArtifactLayout CreateLayout(RunWorkspace workspace, string runId)
    {
        var root = Path.Combine(workspace.ArtifactsDirectory ?? workspace.WorkingDirectory, runId, "sqlite-lookup-series");
        Directory.CreateDirectory(root);
        return new SqliteLookupArtifactLayout(root, Path.Combine(root, "lookup.sqlite"));
    }

    public static SqliteLookupArtifactInventory Collect(SqliteLookupArtifactLayout layout, string relativeRoot)
    {
        var descriptors = new List<ArtifactDescriptor>();
        var total = 0L;
        var primaryDatabase = 0L;
        var wal = 0L;
        var sharedMemory = 0L;

        if (!Directory.Exists(layout.ArtifactsRootDirectory))
        {
            return new SqliteLookupArtifactInventory(descriptors, 0, 0, 0, 0);
        }

        foreach (var file in Directory.EnumerateFiles(layout.ArtifactsRootDirectory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var role = ResolveRole(file);
            var relative = Path.GetRelativePath(relativeRoot, info.FullName);
            descriptors.Add(new ArtifactDescriptor(role, relative, info.Length));
            total += info.Length;
            if (role == ArtifactRole.PrimaryDatabase) primaryDatabase += info.Length;
            if (role == ArtifactRole.Wal) wal += info.Length;
            if (role == ArtifactRole.SharedMemory) sharedMemory += info.Length;
        }

        return new SqliteLookupArtifactInventory(descriptors, total, primaryDatabase, wal, sharedMemory);
    }

    private static ArtifactRole ResolveRole(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.EndsWith("-wal")) return ArtifactRole.Wal;
        if (name.EndsWith("-shm")) return ArtifactRole.SharedMemory;
        if (name.EndsWith(".sqlite") || name.EndsWith(".db")) return ArtifactRole.PrimaryDatabase;
        return ArtifactRole.Unknown;
    }
}
