using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace DevBar.Core.Ports;

/// <summary>
/// Enumerates listening TCP ports and bound UDP ports with owning PIDs via
/// GetExtendedTcpTable / GetExtendedUdpTable (the same API TCPView uses),
/// across both IPv4 and IPv6.
/// </summary>
public static class PortScanner
{
    private const int AF_INET = 2;
    private const int AF_INET6 = 23;
    private const int TCP_TABLE_OWNER_PID_LISTENER = 3;
    private const int UDP_TABLE_OWNER_PID = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] localAddr;
        public uint localScopeId;
        public uint localPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] remoteAddr;
        public uint remoteScopeId;
        public uint remotePort;
        public uint state;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID
    {
        public uint localAddr;
        public uint localPort;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] localAddr;
        public uint localScopeId;
        public uint localPort;
        public uint owningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

    public static List<PortInfo> GetListeningPorts(bool includeUdp = true)
    {
        var results = new List<PortInfo>();
        var processNames = new Dictionary<int, string>();

        foreach (var row in ReadTable<MIB_TCPROW_OWNER_PID>(isTcp: true, AF_INET))
            results.Add(ToPortInfo(PortProtocol.Tcp, new IPAddress(row.localAddr), row.localPort, (int)row.owningPid, processNames));

        foreach (var row in ReadTable<MIB_TCP6ROW_OWNER_PID>(isTcp: true, AF_INET6))
            results.Add(ToPortInfo(PortProtocol.Tcp, new IPAddress(row.localAddr), row.localPort, (int)row.owningPid, processNames));

        if (includeUdp)
        {
            foreach (var row in ReadTable<MIB_UDPROW_OWNER_PID>(isTcp: false, AF_INET))
                results.Add(ToPortInfo(PortProtocol.Udp, new IPAddress(row.localAddr), row.localPort, (int)row.owningPid, processNames));

            foreach (var row in ReadTable<MIB_UDP6ROW_OWNER_PID>(isTcp: false, AF_INET6))
                results.Add(ToPortInfo(PortProtocol.Udp, new IPAddress(row.localAddr), row.localPort, (int)row.owningPid, processNames));
        }

        return results
            .OrderBy(p => p.Port)
            .ThenBy(p => p.Protocol)
            .ThenBy(p => p.LocalAddress, StringComparer.Ordinal)
            .ToList();
    }

    private static PortInfo ToPortInfo(
        PortProtocol proto, IPAddress address, uint portNetOrder, int pid, Dictionary<int, string> nameCache)
    {
        if (!nameCache.TryGetValue(pid, out var name))
        {
            name = GetProcessName(pid);
            nameCache[pid] = name;
        }

        return new PortInfo(proto, address.ToString(), NetworkPortToHost(portNetOrder), pid, name);
    }

    /// <summary>The API returns the port in network byte order packed into the low 16 bits.</summary>
    public static int NetworkPortToHost(uint rawPort)
    {
        return (int)(((rawPort & 0xFF) << 8) | ((rawPort >> 8) & 0xFF));
    }

    private static string GetProcessName(int pid)
    {
        if (pid == 0) return "System Idle";
        if (pid == 4) return "System";
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return "<exited>";
        }
    }

    private static List<T> ReadTable<T>(bool isTcp, int addressFamily) where T : struct
    {
        var rows = new List<T>();
        var size = 0;

        _ = isTcp
            ? GetExtendedTcpTable(IntPtr.Zero, ref size, true, addressFamily, TCP_TABLE_OWNER_PID_LISTENER, 0)
            : GetExtendedUdpTable(IntPtr.Zero, ref size, true, addressFamily, UDP_TABLE_OWNER_PID, 0);

        if (size == 0) return rows;

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var result = isTcp
                ? GetExtendedTcpTable(buffer, ref size, true, addressFamily, TCP_TABLE_OWNER_PID_LISTENER, 0)
                : GetExtendedUdpTable(buffer, ref size, true, addressFamily, UDP_TABLE_OWNER_PID, 0);
            if (result != 0) return rows;

            var count = Marshal.ReadInt32(buffer);
            var rowPtr = buffer + sizeof(int);
            var rowSize = Marshal.SizeOf<T>();
            for (var i = 0; i < count; i++)
            {
                rows.Add(Marshal.PtrToStructure<T>(rowPtr + i * rowSize));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return rows;
    }
}
