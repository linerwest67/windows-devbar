using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DevBar.Core.Network;

public sealed record NicInfo(string Name, string Description, List<string> Addresses, string? Gateway, long SpeedBps);

public sealed record NetworkSnapshot(
    List<NicInfo> Interfaces,
    List<string> DnsServers,
    string? PublicIp);

public static class NetworkInfo
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static NetworkSnapshot GetSnapshot(string? publicIp)
    {
        var nics = new List<NicInfo>();
        var dns = new List<string>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var props = nic.GetIPProperties();
            var addresses = props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .ToList();
            if (addresses.Count == 0) continue;

            var gateway = props.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString();

            foreach (var server in props.DnsAddresses)
            {
                if (server.AddressFamily == AddressFamily.InterNetwork && !dns.Contains(server.ToString()))
                    dns.Add(server.ToString());
            }

            nics.Add(new NicInfo(nic.Name, nic.Description, addresses, gateway, nic.Speed));
        }

        return new NetworkSnapshot(nics, dns, publicIp);
    }

    public static async Task<string?> GetPublicIpAsync()
    {
        try
        {
            var ip = (await Http.GetStringAsync("https://api.ipify.org")).Trim();
            return IPAddress.TryParse(ip, out _) ? ip : null;
        }
        catch
        {
            return null;
        }
    }

    public sealed record PingReplyInfo(bool Success, long RoundtripMs, string Status);

    public static async Task<PingReplyInfo> PingAsync(string host, int timeoutMs = 3000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            return new PingReplyInfo(reply.Status == IPStatus.Success, reply.RoundtripTime, reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return new PingReplyInfo(false, 0, ex.InnerException?.Message ?? ex.Message);
        }
    }
}
