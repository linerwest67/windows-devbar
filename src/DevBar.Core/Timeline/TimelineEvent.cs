namespace DevBar.Core.Timeline;

public enum TimelineCategory
{
    Port,
    Process,
    Docker,
    Automation,
    System,
}

public sealed record TimelineEvent(DateTimeOffset Timestamp, TimelineCategory Category, string Message);
