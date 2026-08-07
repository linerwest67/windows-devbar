using System.ComponentModel;
using System.Diagnostics;

namespace DevBar.Core.Ports;

public enum KillResult
{
    Killed,
    AccessDenied,
    NotFound,
    Failed,
}

public static class ProcessKiller
{
    public static KillResult Kill(int pid)
    {
        if (pid is 0 or 4) return KillResult.AccessDenied; // system processes

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
            return KillResult.Killed;
        }
        catch (ArgumentException)
        {
            return KillResult.NotFound;
        }
        catch (Win32Exception)
        {
            return KillResult.AccessDenied;
        }
        catch (InvalidOperationException)
        {
            return KillResult.NotFound; // exited between lookup and kill
        }
        catch
        {
            return KillResult.Failed;
        }
    }

    /// <summary>Kills every process listening on the given port. Returns per-PID results.</summary>
    public static List<(int Pid, string ProcessName, KillResult Result)> KillByPort(int port)
    {
        var results = new List<(int, string, KillResult)>();
        var matches = PortScanner.GetListeningPorts()
            .Where(p => p.Port == port)
            .DistinctBy(p => p.Pid);

        foreach (var match in matches)
        {
            results.Add((match.Pid, match.ProcessName, Kill(match.Pid)));
        }

        return results;
    }
}
