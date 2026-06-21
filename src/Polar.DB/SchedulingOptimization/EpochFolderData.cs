namespace Polar.DB.SchedulingOptimization;

public sealed class EpochFolderData
{
    public EpochFolderData(string name,
        string path,
        DateTimeOffset utcTime,
        EpochState state)
    {
        this.Name = name;
        this.Path = path;
        this.UtcTime = utcTime;
        this.State = state;
    }

    public string Name { get; }
    public string Path { get; }
    public DateTimeOffset UtcTime { get; }
    public EpochState State { get; }

}