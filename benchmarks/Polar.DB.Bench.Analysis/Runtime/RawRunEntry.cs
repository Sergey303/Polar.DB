using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Couples one raw run payload with its source file path.
/// The payload is the factual run data. The path is needed for traceability in reports.
/// </summary>
internal sealed record RawRunEntry(RunResult Result, string Path);
