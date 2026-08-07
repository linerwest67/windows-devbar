namespace DevBar.Core.Ports;

public enum PortProtocol
{
    Tcp,
    Udp,
}

public sealed record PortInfo(
    PortProtocol Protocol,
    string LocalAddress,
    int Port,
    int Pid,
    string ProcessName);
