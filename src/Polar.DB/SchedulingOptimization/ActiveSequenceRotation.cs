using Polar.Universal;

namespace Polar.DB.SchedulingOptimization;

public sealed class ActiveSequenceRotation
{
    internal ActiveSequenceRotation(USequence source, AppendCollector collector)
    {
        Source = source;
        Collector = collector;
    }

    public USequence Source { get; }
    internal AppendCollector Collector { get; }
}
