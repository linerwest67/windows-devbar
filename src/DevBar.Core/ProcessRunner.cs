using System.Diagnostics;
using System.Text;

namespace DevBar.Core;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

public static class ProcessRunner
{
    /// <summary>Runs a command with no window, capturing output. Returns null if the exe is missing.</summary>
    public static async Task<ProcessResult?> RunAsync(
        string fileName,
        string arguments,
        int timeoutMs = 30_000,
        Encoding? outputEncoding = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = outputEncoding,
            StandardErrorEncoding = outputEncoding,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return null;

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
                return new ProcessResult(-1, await stdOutTask, "timed out");
            }

            return new ProcessResult(process.ExitCode, await stdOutTask, await stdErrTask);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // executable not found
        }
    }
}
