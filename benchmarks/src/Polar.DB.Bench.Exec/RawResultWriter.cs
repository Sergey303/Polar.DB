using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Exec;

public static class RawResultWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Write(RunPaths paths, ExperimentRunResult result)
    {
        if (paths is null)
            throw new ArgumentNullException(nameof(paths));

        if (result is null)
            throw new ArgumentNullException(nameof(result));

        Directory.CreateDirectory(paths.ResultsRawDirectory);

        var json = JsonSerializer.Serialize(result, Options);

        // Atomic-ish write: сначала tmp, потом replace/move.
        var tempPath = paths.RawResultPath + ".tmp";

        File.WriteAllText(tempPath, json);

        if (File.Exists(paths.RawResultPath))
            File.Delete(paths.RawResultPath);

        File.Move(tempPath, paths.RawResultPath);
    }
}