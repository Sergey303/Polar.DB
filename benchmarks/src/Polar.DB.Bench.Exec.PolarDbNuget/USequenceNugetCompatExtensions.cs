using System;
using System.Collections.Generic;

namespace Polar.DB.Bench.Engine.PolarDb;

/// <summary>
/// Compatibility extension used only by the external NuGet runner.
/// Older Polar.DB NuGet packages do not expose USequence.AppendElements.
/// When the pinned package lacks the instance method, the linked adapter binds to this extension
/// and keeps old-package append behavior through repeated AppendElement calls.
/// </summary>
internal static class USequenceNugetCompatExtensions
{
    public static void AppendElements(this Polar.DB.USequence sequence, IEnumerable<object> flow)
    {
        _ = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _ = flow ?? throw new ArgumentNullException(nameof(flow));

        foreach (var element in flow)
        {
            sequence.AppendElement(element);
        }
    }
}
