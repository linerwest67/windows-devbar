using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core;
using DevBar.Core.Export;
using DevBar.Core.Network;
using DevBar.Core.Ports;
using DevBar.Core.Timeline;

namespace DevBar.ViewModels;

public partial class ToolsViewModel : ObservableObject
{
    private readonly App _app;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _outputText = "";
    [ObservableProperty] private bool _isBusy;

    public ToolsViewModel(App app) => _app = app;

    [RelayCommand]
    private async Task FlushDnsAsync()
    {
        StatusText = "Flushing DNS cache…";
        var result = await ProcessRunner.RunAsync("ipconfig", "/flushdns");
        StatusText = result is { Success: true } ? "DNS cache flushed" : "Flush failed";
        _app.Timeline.Add(TimelineCategory.System, "DNS cache flushed");
    }

    [RelayCommand]
    private async Task RenewDhcpAsync()
    {
        StatusText = "Renewing DHCP lease — this can take a few seconds…";
        var result = await ProcessRunner.RunAsync("ipconfig", "/renew", 45_000);
        StatusText = result is { Success: true } ? "DHCP lease renewed" : "Renew failed (static IP?)";
        _app.Timeline.Add(TimelineCategory.System, "DHCP lease renewed");
    }

    [RelayCommand]
    private void RestartExplorer()
    {
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("explorer"))
            {
                using (p) p.Kill();
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
            });
            StatusText = "Explorer restarted";
            _app.Timeline.Add(TimelineCategory.System, "Explorer restarted");
        }
        catch (Exception ex)
        {
            StatusText = $"Could not restart Explorer: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenTempFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", Path.GetTempPath())
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    [RelayCommand]
    private async Task RunDiagnosticAsync(string? tool)
    {
        if (IsBusy || tool is null) return;

        // Fixed tool list — the parameter picks a preset, it is never a command line.
        (string file, string args) = tool switch
        {
            "ipconfig" => ("ipconfig", "/all"),
            "routes" => ("route", "print -4"),
            "arp" => ("arp", "-a"),
            "dns" => ("ipconfig", "/displaydns"),
            _ => ("", ""),
        };
        if (file.Length == 0) return;

        IsBusy = true;
        StatusText = $"Running {file} {args}…";
        try
        {
            var result = await ProcessRunner.RunAsync(file, args);
            OutputText = result?.StdOut.Trim() ?? "command unavailable";
            StatusText = $"{file} {args}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportSnapshotAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"devbar-snapshot-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        StatusText = "Exporting…";
        try
        {
            var json = await Task.Run(() =>
            {
                var vitals = _app.SampleVitals();
                var ports = PortScanner.GetListeningPorts();
                var network = NetworkInfo.GetSnapshot(publicIp: null);
                return SnapshotExporter.ToJson(vitals, ports, network);
            });
            await File.WriteAllTextAsync(dialog.FileName, json);
            StatusText = $"Snapshot saved to {Path.GetFileName(dialog.FileName)}";
            _app.Timeline.Add(TimelineCategory.System, $"Snapshot exported to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
    }
}
