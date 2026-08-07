using System.Collections.ObjectModel;

namespace DevBar.Core.Timeline;

/// <summary>In-memory ring buffer of recent events, newest first.</summary>
public sealed class TimelineStore
{
    private const int MaxEvents = 500;
    private readonly object _lock = new();
    private readonly LinkedList<TimelineEvent> _events = new();

    public event Action<TimelineEvent>? EventAdded;

    public void Add(TimelineCategory category, string message)
    {
        var evt = new TimelineEvent(DateTimeOffset.Now, category, message);
        lock (_lock)
        {
            _events.AddFirst(evt);
            while (_events.Count > MaxEvents) _events.RemoveLast();
        }
        EventAdded?.Invoke(evt);
    }

    public ReadOnlyCollection<TimelineEvent> Snapshot()
    {
        lock (_lock)
        {
            return _events.ToList().AsReadOnly();
        }
    }
}
