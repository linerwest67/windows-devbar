using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.HostsFile;

namespace DevBar.ViewModels;

public partial class HostsViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _newIp = "127.0.0.1";
    [ObservableProperty] private string _newHost = "";

    public ObservableCollection<HostsEntry> Entries { get; } = [];

    public void Refresh()
    {
        try
        {
            Entries.Clear();
            foreach (var entry in HostsFileService.Read()) Entries.Add(entry);
            StatusText = $"{Entries.Count} entries";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not read hosts file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Add()
    {
        var ip = NewIp.Trim();
        var host = NewHost.Trim();
        if (ip.Length == 0 || host.Length == 0) return;
        if (!System.Net.IPAddress.TryParse(ip, out _))
        {
            StatusText = $"'{ip}' is not a valid IP address";
            return;
        }

        if (!HostsFileService.IsValidHostName(host))
        {
            StatusText = $"'{host}' is not a valid hostname (letters, digits, dots, hyphens)";
            return;
        }

        var updated = Entries.ToList();
        updated.Add(new HostsEntry(ip, host, true, "added by DevBar"));
        Write(updated, $"Added {host} → {ip}");
        NewHost = "";
    }

    [RelayCommand]
    private void Remove(HostsEntry? entry)
    {
        if (entry is null) return;
        var updated = Entries.Where(x => x != entry).ToList();
        Write(updated, $"Removed {entry.HostName}");
    }

    [RelayCommand]
    private void Toggle(HostsEntry? entry)
    {
        if (entry is null) return;
        var updated = Entries
            .Select(x => x == entry ? x with { Enabled = !x.Enabled } : x)
            .ToList();
        Write(updated, $"{(entry.Enabled ? "Disabled" : "Enabled")} {entry.HostName}");
    }

    private void Write(List<HostsEntry> entries, string successMessage)
    {
        StatusText = "Waiting for administrator approval…";
        if (HostsFileService.WriteElevated(entries))
        {
            StatusText = successMessage;
            Refresh();
        }
        else
        {
            StatusText = "Change cancelled (UAC declined or write failed)";
        }
    }

    [RelayCommand]
    private void OpenInNotepad()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", HostsFileService.HostsPath)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}
