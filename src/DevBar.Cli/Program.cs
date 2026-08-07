using DevBar.Core.Export;
using DevBar.Core.Ipc;
using DevBar.Core.Network;
using DevBar.Core.Ports;
using DevBar.Core.Vitals;
using DevBar.Core.Wsl;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

var command = args[0].ToLowerInvariant();
var arg = args.Length > 1 ? args[1] : "";

switch (command)
{
    case "vitals":
        return await ForwardOrLocal(IpcProtocol.VerbVitals, LocalVitals);

    case "ports":
        return await ForwardOrLocal(IpcProtocol.VerbPorts, LocalPorts);

    case "kill":
        if (!int.TryParse(arg, out var port))
        {
            Console.Error.WriteLine("usage: devbar kill <port>");
            return 2;
        }
        return await ForwardOrLocal($"{IpcProtocol.VerbKillPort} {port}", () => LocalKill(port));

    case "wsl":
        if (!arg.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("usage: devbar wsl list");
            return 2;
        }
        return await ForwardOrLocal(IpcProtocol.VerbWslList, LocalWslList);

    case "open":
    {
        var tab = arg.Length > 0 ? arg : "vitals";
        var response = await PipeClient.SendAsync($"{IpcProtocol.VerbOpenTab} {tab}");
        if (response is null)
        {
            Console.Error.WriteLine("DevBar is not running.");
            return 1;
        }
        return 0;
    }

    case "export":
    {
        var json = await PipeClient.SendAsync(IpcProtocol.VerbExport) ?? LocalExport();

        // devbar export -o snapshot.json writes a file; no -o prints to stdout.
        if (arg == "-o" && args.Length > 2)
        {
            await File.WriteAllTextAsync(args[2], json);
            Console.WriteLine($"Snapshot written to {args[2]}");
        }
        else
        {
            Console.WriteLine(json);
        }
        return 0;
    }

    default:
        Console.Error.WriteLine($"unknown command: {command}");
        PrintUsage();
        return 2;
}

// Prefer the running app (its data is already warm); fall back to a direct local read.
async Task<int> ForwardOrLocal(string request, Func<Task<string>> localFallback)
{
    var response = await PipeClient.SendAsync(request);
    Console.WriteLine(response ?? await localFallback());
    return 0;
}

static Task<string> LocalVitals()
{
    using var sampler = new SystemSampler(enableHardwareSensors: false);
    sampler.Sample();               // prime the CPU counter
    Thread.Sleep(500);
    var v = sampler.Sample();
    return Task.FromResult(
        $"CPU {v.CpuPercent:F0}%  RAM {v.MemoryPercent:F0}% " +
        $"({v.MemoryUsedBytes / 1073741824.0:F1}/{v.MemoryTotalBytes / 1073741824.0:F1} GiB)  " +
        $"Up {v.Uptime.Days}d{v.Uptime.Hours}h{v.Uptime.Minutes}m");
}

static Task<string> LocalPorts()
{
    var ports = PortScanner.GetListeningPorts();
    return Task.FromResult(string.Join('\n', ports.Select(p =>
        $"{p.Protocol,-4} {p.LocalAddress,-15} :{p.Port,-6} {p.ProcessName} ({p.Pid})")));
}

static Task<string> LocalKill(int port)
{
    var results = ProcessKiller.KillByPort(port);
    return Task.FromResult(results.Count == 0
        ? $"nothing listening on :{port}"
        : string.Join('\n', results.Select(r => $"{r.ProcessName} (pid {r.Pid}): {r.Result}")));
}

static string LocalExport()
{
    using var sampler = new SystemSampler(enableHardwareSensors: false);
    sampler.Sample();
    Thread.Sleep(500);
    var vitals = sampler.Sample();
    return SnapshotExporter.ToJson(vitals, PortScanner.GetListeningPorts(), NetworkInfo.GetSnapshot(publicIp: null));
}

static async Task<string> LocalWslList()
{
    var distros = await WslService.GetDistrosAsync();
    if (distros is null) return "wsl not available";
    return string.Join('\n', distros.Select(d =>
        $"{(d.IsDefault ? "*" : " ")} {d.Name,-20} {d.State,-10} v{d.Version}"));
}

static void PrintUsage()
{
    Console.WriteLine("""
        devbar — Windows DevBar CLI

          devbar vitals              CPU, RAM and uptime summary
          devbar ports               listening TCP/UDP ports with owning processes
          devbar kill <port>         kill whatever is listening on <port>
          devbar wsl list            WSL distros and their states
          devbar open [tab]          focus the DevBar popup on a tab
          devbar export [-o file]    machine snapshot (vitals+ports+network) as JSON

        Commands talk to the running DevBar app when it is open, and fall back
        to reading the system directly when it is not.
        """);
}
