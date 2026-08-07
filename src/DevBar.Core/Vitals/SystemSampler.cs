using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace DevBar.Core.Vitals;

/// <summary>
/// Samples system vitals on demand. Create once, call <see cref="Sample"/> periodically.
/// Hardware sensors (GPU/temps/fans) are best-effort: they typically need admin rights,
/// and all sensor values are null when unavailable.
/// </summary>
public sealed class SystemSampler : IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly Computer? _computer;
    private long _lastRxBytes;
    private long _lastTxBytes;
    private DateTime _lastNetSample = DateTime.MinValue;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    public SystemSampler(bool enableHardwareSensors = true)
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // first read always returns 0
        }
        catch
        {
            _cpuCounter = null;
        }

        if (enableHardwareSensors)
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                };
                _computer.Open();
            }
            catch
            {
                _computer = null;
            }
        }
    }

    public VitalsSnapshot Sample()
    {
        var (rx, tx) = SampleNetwork();
        var (batteryPercent, charging) = SampleBattery();
        var (gpuPct, cpuTemp, gpuTemp, fanRpm) = SampleSensors();

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        long memTotal = 0, memUsed = 0;
        if (GlobalMemoryStatusEx(ref mem))
        {
            memTotal = (long)mem.ullTotalPhys;
            memUsed = (long)(mem.ullTotalPhys - mem.ullAvailPhys);
        }

        return new VitalsSnapshot
        {
            CpuPercent = SampleCpu(),
            MemoryTotalBytes = memTotal,
            MemoryUsedBytes = memUsed,
            Drives = SampleDrives(),
            NetworkRxBytesPerSec = rx,
            NetworkTxBytesPerSec = tx,
            BatteryPercent = batteryPercent,
            BatteryCharging = charging,
            Uptime = TimeSpan.FromMilliseconds(GetTickCount64()),
            GpuPercent = gpuPct,
            CpuTempCelsius = cpuTemp,
            GpuTempCelsius = gpuTemp,
            FanRpm = fanRpm,
        };
    }

    private double SampleCpu()
    {
        try
        {
            return _cpuCounter is null ? 0 : Math.Clamp(_cpuCounter.NextValue(), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static List<DriveUsage> SampleDrives()
    {
        var drives = new List<DriveUsage>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType == DriveType.Fixed && d.IsReady)
                    drives.Add(new DriveUsage(d.Name, d.TotalSize, d.TotalFreeSpace));
            }
            catch
            {
                // Drive vanished between enumeration and read.
            }
        }
        return drives;
    }

    private (double rx, double tx) SampleNetwork()
    {
        try
        {
            long rxTotal = 0, txTotal = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var stats = nic.GetIPStatistics();
                rxTotal += stats.BytesReceived;
                txTotal += stats.BytesSent;
            }

            var now = DateTime.UtcNow;
            double rxRate = 0, txRate = 0;
            if (_lastNetSample != DateTime.MinValue)
            {
                var secs = (now - _lastNetSample).TotalSeconds;
                if (secs > 0)
                {
                    rxRate = Math.Max(0, (rxTotal - _lastRxBytes) / secs);
                    txRate = Math.Max(0, (txTotal - _lastTxBytes) / secs);
                }
            }

            _lastRxBytes = rxTotal;
            _lastTxBytes = txTotal;
            _lastNetSample = now;
            return (rxRate, txRate);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static (int? percent, bool? charging) SampleBattery()
    {
        if (!GetSystemPowerStatus(out var status)) return (null, null);
        if (status.BatteryFlag == 128 /* no battery */ || status.BatteryLifePercent == 255) return (null, null);
        return (status.BatteryLifePercent, status.ACLineStatus == 1);
    }

    private (double? gpu, double? cpuTemp, double? gpuTemp, double? fan) SampleSensors()
    {
        if (_computer is null) return (null, null, null, null);

        double? gpuLoad = null, cpuTemp = null, gpuTemp = null, fan = null;
        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                var isGpu = hw.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.Value is not { } value) continue;
                    switch (sensor.SensorType)
                    {
                        case SensorType.Load when isGpu && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase):
                            gpuLoad ??= value;
                            break;
                        // Without admin rights the sensor exists but reads 0; that is
                        // "no reading", not a real temperature.
                        case SensorType.Temperature when hw.HardwareType == HardwareType.Cpu && value > 0:
                            cpuTemp = cpuTemp is null ? value : Math.Max(cpuTemp.Value, value);
                            break;
                        case SensorType.Temperature when isGpu && value > 0:
                            gpuTemp ??= value;
                            break;
                        case SensorType.Fan when value > 0:
                            fan ??= value;
                            break;
                    }
                }
            }
        }
        catch
        {
            // Sensor read failures are non-fatal; report what we have.
        }

        return (gpuLoad, cpuTemp, gpuTemp, fan);
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        try
        {
            _computer?.Close();
        }
        catch
        {
        }
    }
}
