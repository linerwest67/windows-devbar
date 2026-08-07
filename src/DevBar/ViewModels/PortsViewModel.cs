using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.Ports;
using DevBar.Core.Timeline;

namespace DevBar.ViewModels;

public partial class PortsViewModel : ObservableObject
{
    // Dev-stack processes surface above system noise, same ranking idea mac-devbar uses.
    private static readonly HashSet<string> DevProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "bun", "deno", "vite", "next", "webpack", "python", "python3", "ruby",
        "dotnet", "java", "php", "go", "cargo", "docker", "com.docker.backend",
        "nginx", "caddy", "redis-server", "postgres", "mysqld", "mongod", "code",
    };

    private readonly App _app;
    private List<PortInfo> _allPorts = [];

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<PortInfo> Ports { get; } = [];

    public PortsViewModel(App app) => _app = app;

    public void Apply(List<PortInfo> ports)
    {
        _allPorts = ports;
        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = FilterText.Trim();
        var filtered = query.Length == 0
            ? _allPorts
            : _allPorts.Where(p =>
                p.Port.ToString().Contains(query) ||
                p.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        var ranked = filtered
            .OrderByDescending(p => DevProcesses.Contains(TrimExe(p.ProcessName)))
            .ThenBy(p => p.Port)
            .ToList();

        Ports.Clear();
        foreach (var p in ranked) Ports.Add(p);
        StatusText = $"{ranked.Count} of {_allPorts.Count} listeners · dev tools ranked first";
    }

    private static string TrimExe(string processName)
        => processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

    [RelayCommand]
    private void CopyPort(PortInfo? port)
    {
        if (port is null) return;
        try
        {
            System.Windows.Clipboard.SetText(port.Port.ToString());
            StatusText = $"Copied {port.Port} to clipboard";
        }
        catch
        {
            StatusText = "Clipboard is busy — try again";
        }
    }

    [RelayCommand]
    private void OpenInBrowser(PortInfo? port)
    {
        if (port is null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                $"http://localhost:{port.Port}/")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            StatusText = "Could not open the browser";
        }
    }

    [RelayCommand]
    private void Kill(PortInfo? port)
    {
        if (port is null) return;
        var result = ProcessKiller.Kill(port.Pid);
        _app.Timeline.Add(TimelineCategory.Process, $"Kill {port.ProcessName} (pid {port.Pid}) → {result}");
        StatusText = result switch
        {
            KillResult.Killed => $"Killed {port.ProcessName} (pid {port.Pid})",
            KillResult.AccessDenied => $"Access denied killing {port.ProcessName} — try running DevBar as administrator",
            KillResult.NotFound => $"{port.ProcessName} already exited",
            _ => $"Failed to kill {port.ProcessName}",
        };
    }

    public void KillByPortNumber(int port)
    {
        var results = ProcessKiller.KillByPort(port);
        StatusText = results.Count == 0
            ? $"Nothing listening on :{port}"
            : string.Join("; ", results.Select(r => $"{r.ProcessName}: {r.Result}"));
    }
}
