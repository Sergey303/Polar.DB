using System;
using System.Collections.Generic;
using System.IO;

namespace Polar.DB.Bench.Exec;

public static class ArtifactScanner
{
    public static List<ArtifactInfo> ScanEngineArtifacts(RunPaths paths)
    {
        if (paths is null)
            throw new ArgumentNullException(nameof(paths));

        var result = new List<ArtifactInfo>();

        ScanDirectory(paths.WorkDirectory, result);
        ScanDirectory(paths.DataDirectory, result);
        ScanDirectory(paths.ArtifactsDirectory, result);

        return result;
    }

    private static void ScanDirectory(string directory, List<ArtifactInfo> result)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);

            result.Add(new ArtifactInfo
            {
                Path = file,
                Role = GuessRole(file),
                Bytes = info.Exists ? info.Length : 0
            });
        }
    }

    private static string GuessRole(string path)
    {
        var fileName = Path.GetFileName(path);

        if (fileName.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sqlite-wal", StringComparison.OrdinalIgnoreCase))
            return "wal";

        if (fileName.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sqlite-shm", StringComparison.OrdinalIgnoreCase))
            return "shared-memory";

        if (fileName.EndsWith(".state", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pdbstate", StringComparison.OrdinalIgnoreCase))
            return "state";

        if (fileName.EndsWith(".index", StringComparison.OrdinalIgnoreCase))
            return "secondary-index";

        if (fileName.Equals("polar.db", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".polar.db", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pdbbin", StringComparison.OrdinalIgnoreCase))
            return "polar-primary-data";

        if (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
            return "primary-db";

        return "other";
    }
}