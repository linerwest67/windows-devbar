using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.Network;

namespace DevBar.ViewModels;

public partial class NetworkViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty] private string _publicIp = "…";
    [ObservableProperty] private string _dnsServers = "";
    [ObservableProperty] private string _pingHost = "1.1.1.1";
    [ObservableProperty] private string _pingResult = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<NicInfo> Interfaces { get; } = [];

    public async void Refresh()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var publicIp = await NetworkInfo.GetPublicIpAsync();
            var snapshot = NetworkInfo.GetSnapshot(publicIp);

            Interfaces.Clear();
            foreach (var nic in snapshot.Interfaces) Interfaces.Add(nic);
            DnsServers = string.Join(", ", snapshot.DnsServers);
            PublicIp = snapshot.PublicIp ?? "offline?";
        }
        catch (Exception ex)
        {
            PublicIp = "error";
            DnsServers = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PingAsync()
    {
        var host = PingHost.Trim();
        if (host.Length == 0) return;
        PingResult = "pinging…";
        var reply = await NetworkInfo.PingAsync(host);
        PingResult = reply.Success ? $"{host}: {reply.RoundtripMs} ms" : $"{host}: {reply.Status}";
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
            // Clipboard can be locked by another process; nothing useful to do.
        }
    }
}
