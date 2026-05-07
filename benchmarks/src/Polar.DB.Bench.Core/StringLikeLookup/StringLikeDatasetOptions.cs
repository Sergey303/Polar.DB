using System;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record StringLikeDatasetOptions(
    int RecordCount,
    int GroupCount,
    int SubGroupCount,
    int PayloadBytes,
    int Seed)
{
    public void Validate()
    {
        if (RecordCount <= 0) throw new ArgumentOutOfRangeException(nameof(RecordCount));
        if (GroupCount <= 0) throw new ArgumentOutOfRangeException(nameof(GroupCount));
        if (SubGroupCount <= 0) throw new ArgumentOutOfRangeException(nameof(SubGroupCount));
        if (PayloadBytes < 0) throw new ArgumentOutOfRangeException(nameof(PayloadBytes));
    }
}
