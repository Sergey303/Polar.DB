namespace Polar.DB.Bench.Engine.PolarDb.StringLikeLookup;

public sealed record PolarDbLikeLookupEngineOptions(
    string WorkDirectory,
    string EngineKey = "polar-db-current");
