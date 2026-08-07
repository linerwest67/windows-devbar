using DevBar.Core.Ports;

namespace DevBar.Core.Tests;

public class PortScannerTests
{
    [Theory]
    [InlineData(0x5000u, 80)]      // 0x0050 network order → 80
    [InlineData(0xB80Bu, 3000)]    // 0x0BB8 → 3000
    [InlineData(0x1F90u, 36895)]
    public void NetworkPortToHost_SwapsByteOrder(uint raw, int expected)
    {
        Assert.Equal(expected, PortScanner.NetworkPortToHost(raw));
    }

    [Fact]
    public void GetListeningPorts_ReturnsRealListenersWithPids()
    {
        var ports = PortScanner.GetListeningPorts();

        // A Windows machine always has some listeners (RPC, SMB, etc.)
        Assert.NotEmpty(ports);
        Assert.All(ports, p =>
        {
            Assert.InRange(p.Port, 1, 65535);
            Assert.True(p.Pid >= 0);
            Assert.False(string.IsNullOrWhiteSpace(p.ProcessName));
        });
    }
}
