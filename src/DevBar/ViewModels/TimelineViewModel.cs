using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using DevBar.Core.Timeline;

namespace DevBar.ViewModels;

public partial class TimelineViewModel : ObservableObject, IRefreshable
{
    private readonly TimelineStore _store;

    public ObservableCollection<TimelineEvent> Events { get; } = [];

    public TimelineViewModel(TimelineStore store)
    {
        _store = store;
        _store.EventAdded += evt =>
            Application.Current?.Dispatcher.BeginInvoke(() => Events.Insert(0, evt));
        Refresh();
    }

    public void Refresh()
    {
        Events.Clear();
        foreach (var evt in _store.Snapshot()) Events.Add(evt);
    }
}
