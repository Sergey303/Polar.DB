namespace Polar.DB.SchedulingOptimization;

public sealed record EpochFolderData
{
    public EpochFolderData(string Name,
        string Path,
        DateTimeOffset UtcTime,
        EpochState State)
    {
        this.Name = Name;
        this.Path = Path;
        this.UtcTime = UtcTime;
        this.State = State;
    }

    public string Name { get; }
    public string Path { get; }
    public DateTimeOffset UtcTime { get; }
    public EpochState State { get; }

}