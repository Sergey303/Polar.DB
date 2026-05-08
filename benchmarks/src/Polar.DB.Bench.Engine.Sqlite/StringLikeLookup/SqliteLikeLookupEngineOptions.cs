using System.IO;

namespace Polar.DB.Bench.Engine.Sqlite.StringLikeLookup;

public sealed record SqliteLikeLookupEngineOptions(
    string WorkDirectory,
    string DatabaseFileName = "string-like-lookup.sqlite",
    bool UseWal = true,
    bool SynchronousNormal = true)
{
    public string DatabasePath => Path.Combine(WorkDirectory, DatabaseFileName);
}
