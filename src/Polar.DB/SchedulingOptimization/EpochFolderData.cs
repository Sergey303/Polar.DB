namespace Polar.DB.SchedulingOptimization;

public sealed class EpochFolderData
{
    public EpochFolderData(
        string name,
        string path,
        DateTimeOffset utcTime,
        EpochState state)
    {
        Name = name;
        Path = path;
        UtcTime = utcTime;
        State = state;
    }

    public string Name { get; }
    public string Path { get; }
    public DateTimeOffset UtcTime { get; }
    public EpochState State { get; }
}
