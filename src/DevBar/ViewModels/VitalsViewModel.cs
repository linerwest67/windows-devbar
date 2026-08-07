using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.Vitals;
using Microsoft.Win32;

namespace DevBar.ViewModels;

public sealed record MachineFact(string Label, string Value);

public partial class VitalsViewModel : ObservableObject
{
    private const int HistoryCapacity = 90; // ~3 minutes at the default 2 s interval

    private readonly List<double> _cpuHistory = [];
    private readonly List<double> _memHistory = [];
    private readonly List<double> _rxHistory = [];
    private readonly List<double> _txHistory = [];

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _memoryPercent;
    [ObservableProperty] private string _memoryDetail = "";
    [ObservableProperty] private string _downRateText = "";
    [ObservableProperty] private string _upRateText = "";
    [ObservableProperty] private string _uptimeText = "";
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private string _sensorsText = "";

    [ObservableProperty] private string _weatherGlyph = "·";
    [ObservableProperty] private string _weatherLabel = "Calm";
    [ObservableProperty] private string _weatherDetail = "Nothing much happening.";

    [ObservableProperty] private IReadOnlyList<double> _cpuHistoryValues = [];
    [ObservableProperty] private IReadOnlyList<double> _memHistoryValues = [];
    [ObservableProperty] private IReadOnlyList<double> _rxHistoryValues = [];
    [ObservableProperty] private IReadOnlyList<double> _txHistoryValues = [];

    public ObservableCollection<DriveUsage> Drives { get; } = [];
    public List<MachineFact> MachineFacts { get; } = BuildMachineFacts();

    public string WeatherText => $"{WeatherGlyph} {WeatherLabel}";

    public void Apply(VitalsSnapshot snapshot)
    {
        CpuPercent = snapshot.CpuPercent;
        MemoryPercent = snapshot.MemoryPercent;
        MemoryDetail = $"{Format.Bytes(snapshot.MemoryUsedBytes)} / {Format.Bytes(snapshot.MemoryTotalBytes)}";
        DownRateText = $"{Format.Bytes((long)snapshot.NetworkRxBytesPerSec)}/s";
        UpRateText = $"{Format.Bytes((long)snapshot.NetworkTxBytesPerSec)}/s";
        UptimeText = $"{snapshot.Uptime.Days}d {snapshot.Uptime.Hours}h {snapshot.Uptime.Minutes}m";

        Push(_cpuHistory, snapshot.CpuPercent);
        Push(_memHistory, snapshot.MemoryPercent);
        Push(_rxHistory, snapshot.NetworkRxBytesPerSec);
        Push(_txHistory, snapshot.NetworkTxBytesPerSec);
        CpuHistoryValues = [.. _cpuHistory];
        MemHistoryValues = [.. _memHistory];
        RxHistoryValues = [.. _rxHistory];
        TxHistoryValues = [.. _txHistory];

        UpdateWeather(snapshot);

        BatteryText = snapshot.BatteryPercent is { } pct
            ? $"{pct}%{(snapshot.BatteryCharging == true ? " ⚡" : "")}"
            : "";

        var sensors = new List<string>();
        if (snapshot.CpuTempCelsius is { } ct) sensors.Add($"CPU {ct:F0}°C");
        if (snapshot.GpuTempCelsius is { } gt) sensors.Add($"GPU {gt:F0}°C");
        if (snapshot.GpuPercent is { } gp) sensors.Add($"GPU {gp:F0}%");
        if (snapshot.FanRpm is { } fan) sensors.Add($"Fan {fan:F0} rpm");
        SensorsText = sensors.Count > 0 ? string.Join("  ·  ", sensors) : "Sensors unavailable (run as admin for temps/fans)";

        Drives.Clear();
        foreach (var d in snapshot.Drives) Drives.Add(d);
    }

    private static void Push(List<double> buffer, double value)
    {
        buffer.Add(value);
        if (buffer.Count > HistoryCapacity) buffer.RemoveAt(0);
    }

    /// <summary>
    /// Machine weather: the whole picture in one glyph, same scale mac-devbar uses.
    /// Busy network traffic can lift the score a level even when the CPU is quiet.
    /// </summary>
    private void UpdateWeather(VitalsSnapshot s)
    {
        var score = s.CpuPercent;
        var netBusy = s.NetworkRxBytesPerSec + s.NetworkTxBytesPerSec > 5_000_000; // > 5 MB/s
        if (netBusy) score = Math.Max(score, 45);

        (WeatherGlyph, WeatherLabel, WeatherDetail) = score switch
        {
            < 15 => ("·", "Calm", "Nothing much happening."),
            < 40 => ("~", "Breezy", "Light activity on CPU or network."),
            < 75 => ("≈", "Busy", "Sustained load — builds or heavy apps running."),
            _ => ("⚡", "Stormy", "Heavy load. Check Ports or Task Manager if unexpected."),
        };
        OnPropertyChanged(nameof(WeatherText));
    }

    private static List<MachineFact> BuildMachineFacts()
    {
        var facts = new List<MachineFact>
        {
            new("Host", Environment.MachineName),
            new("OS", RuntimeInformation.OSDescription.Replace("Microsoft ", "")),
            new("Arch", RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()),
        };

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("ProcessorNameString") is string cpu)
                facts.Insert(1, new MachineFact("CPU", cpu.Trim()));
        }
        catch
        {
            // Registry read denied — skip the CPU row rather than fail the tab.
        }

        facts.Add(new MachineFact("Cores", Environment.ProcessorCount.ToString()));
        return facts;
    }

    [RelayCommand]
    private void Copy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard can be held by another process; not worth surfacing.
        }
    }
}

public static class Format
{
    public static string Bytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GiB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MiB",
            >= 1024 => $"{bytes / 1024.0:F1} KiB",
            _ => $"{bytes} B",
        };
    }
}
