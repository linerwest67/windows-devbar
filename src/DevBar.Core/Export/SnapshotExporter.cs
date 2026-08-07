using System.Runtime.InteropServices;
using System.Text.Json;
using DevBar.Core.Network;
using DevBar.Core.Ports;
using DevBar.Core.Vitals;

namespace DevBar.Core.Export;

/// <summary>
/// Serializes a point-in-time snapshot of the machine (vitals + ports + network)
/// to JSON. Used by the Tools tab, the EXPORT pipe verb, and `devbar export`.
/// </summary>
public static class SnapshotExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToJson(VitalsSnapshot? vitals, IReadOnlyList<PortInfo> ports, NetworkSnapshot network)
    {
        var snapshot = new
        {
            Timestamp = DateTimeOffset.Now,
            Machine = new
            {
                Host = Environment.MachineName,
                Os = RuntimeInformation.OSDescription,
                Arch = RuntimeInformation.OSArchitecture.ToString(),
                Cores = Environment.ProcessorCount,
            },
            Vitals = vitals,
            Ports = ports,
            Network = network,
        };
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }
}
