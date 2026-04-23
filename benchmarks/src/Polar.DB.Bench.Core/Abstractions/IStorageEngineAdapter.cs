using System.Collections.Generic;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.Abstractions;

public interface IStorageEngineAdapter
{
    string EngineKey { get; }
    IReadOnlyCollection<EngineCapability> Capabilities { get; }
    IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace);
}
