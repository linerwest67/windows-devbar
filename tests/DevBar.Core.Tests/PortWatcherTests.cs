using DevBar.Core.Ports;

namespace DevBar.Core.Tests;

public class PortWatcherTests
{
    private static PortInfo Port(int port, int pid, string address = "0.0.0.0", PortProtocol proto = PortProtocol.Tcp)
        => new(proto, address, port, pid, $"proc{pid}");

    [Fact]
    public void Update_FirstScanIsBaselineAndReportsNothing()
    {
        var watcher = new PortWatcher();

        Assert.Empty(watcher.Update([Port(3000, 100), Port(5432, 200)]));
    }

    [Fact]
    public void Update_HandlesSameProtocolPortAndPidOnDifferentAddresses()
    {
        // One process binding the same UDP port on several NICs, plus dual-stack
        // IPv4/IPv6 listeners, all share (protocol, port, pid).
        List<PortInfo> scan =
        [
            Port(137, 4, "10.0.0.1", PortProtocol.Udp),
            Port(137, 4, "192.168.1.5", PortProtocol.Udp),
            Port(137, 4, "172.18.160.1", PortProtocol.Udp),
            Port(443, 900, "0.0.0.0"),
            Port(443, 900, "::"),
        ];

        var watcher = new PortWatcher();

        Assert.Empty(watcher.Update(scan));   // baseline must not throw
        Assert.Empty(watcher.Update(scan));   // steady state reports no changes
    }

    [Fact]
    public void Update_DetectsOpenedPort()
    {
        var watcher = new PortWatcher();
        watcher.Update([Port(3000, 100)]);

        var changes = watcher.Update([Port(3000, 100), Port(8080, 300)]);

        var change = Assert.Single(changes);
        Assert.True(change.Opened);
        Assert.Equal(8080, change.Port.Port);
    }

    [Fact]
    public void Update_DetectsClosedPortAndKeepsItsProcessName()
    {
        var watcher = new PortWatcher();
        watcher.Update([Port(3000, 100), Port(8080, 300)]);

        var changes = watcher.Update([Port(3000, 100)]);

        var change = Assert.Single(changes);
        Assert.False(change.Opened);
        Assert.Equal(8080, change.Port.Port);
        Assert.Equal("proc300", change.Port.ProcessName);
    }

    [Fact]
    public void Update_ClosingOneAddressOfADualStackListenerReportsOnlyThatOne()
    {
        var watcher = new PortWatcher();
        watcher.Update([Port(443, 900, "0.0.0.0"), Port(443, 900, "::")]);

        var changes = watcher.Update([Port(443, 900, "0.0.0.0")]);

        var change = Assert.Single(changes);
        Assert.False(change.Opened);
        Assert.Equal("::", change.Port.LocalAddress);
    }
}
