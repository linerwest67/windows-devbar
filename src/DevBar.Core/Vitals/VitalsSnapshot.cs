namespace DevBar.Core.Vitals;

public sealed record DriveUsage(string Name, long TotalBytes, long FreeBytes)
{
    public double UsedPercent => TotalBytes == 0 ? 0 : 100.0 * (TotalBytes - FreeBytes) / TotalBytes;
}

public sealed record VitalsSnapshot
{
    public double CpuPercent { get; init; }
    public long MemoryTotalBytes { get; init; }
    public long MemoryUsedBytes { get; init; }
    public double MemoryPercent => MemoryTotalBytes == 0 ? 0 : 100.0 * MemoryUsedBytes / MemoryTotalBytes;
    public IReadOnlyList<DriveUsage> Drives { get; init; } = [];
    public double NetworkRxBytesPerSec { get; init; }
    public double NetworkTxBytesPerSec { get; init; }
    public int? BatteryPercent { get; init; }
    public bool? BatteryCharging { get; init; }
    public TimeSpan Uptime { get; init; }

    // Best-effort sensor extras (null when unavailable, e.g. not running as admin)
    public double? GpuPercent { get; init; }
    public double? CpuTempCelsius { get; init; }
    public double? GpuTempCelsius { get; init; }
    public double? FanRpm { get; init; }
}
