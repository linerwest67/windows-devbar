using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.Settings;

namespace DevBar.ViewModels;

public partial class ClockItemViewModel : ObservableObject
{
    public string ZoneId { get; }
    public string Label { get; }
    private readonly TimeZoneInfo? _zone;

    [ObservableProperty] private string _timeText = "";
    [ObservableProperty] private string _offsetText = "";

    public ClockItemViewModel(string zoneId)
    {
        ZoneId = zoneId;
        if (zoneId == "Local")
        {
            _zone = TimeZoneInfo.Local;
            Label = $"Local ({TimeZoneInfo.Local.Id})";
        }
        else
        {
            try
            {
                _zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                Label = zoneId;
            }
            catch
            {
                _zone = null;
                Label = $"{zoneId} (unknown)";
            }
        }
        Tick();
    }

    public void Tick()
    {
        if (_zone is null)
        {
            TimeText = "--:--";
            return;
        }
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _zone);
        TimeText = now.ToString("HH:mm:ss");
        OffsetText = $"UTC{(now.Offset >= TimeSpan.Zero ? "+" : "-")}{now.Offset:hh\\:mm} · {now:ddd d MMM}";
    }
}

public partial class ClocksViewModel : ObservableObject, IRefreshable
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;

    public ObservableCollection<ClockItemViewModel> Clocks { get; } = [];
    public List<string> AvailableZones { get; }

    [ObservableProperty] private string _selectedZoneToAdd = "";

    public ClocksViewModel(AppSettings settings)
    {
        _settings = settings;
        AvailableZones = TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).ToList();
        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            foreach (var clock in Clocks) clock.Tick();
        };
        _timer.Start();
    }

    public void Refresh()
    {
        Clocks.Clear();
        foreach (var zoneId in _settings.ClockTimeZones) Clocks.Add(new ClockItemViewModel(zoneId));
    }

    [RelayCommand]
    private void AddClock()
    {
        var zone = SelectedZoneToAdd.Trim();
        if (zone.Length == 0 || _settings.ClockTimeZones.Contains(zone)) return;
        _settings.ClockTimeZones.Add(zone);
        _settings.Save();
        Refresh();
    }

    [RelayCommand]
    private void RemoveClock(ClockItemViewModel? clock)
    {
        if (clock is null) return;
        _settings.ClockTimeZones.Remove(clock.ZoneId);
        _settings.Save();
        Refresh();
    }
}
