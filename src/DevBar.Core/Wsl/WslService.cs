using System.Text;

namespace DevBar.Core.Wsl;

public sealed record WslDistro(string Name, string State, int Version, bool IsDefault);

public static class WslService
{
    public static async Task<List<WslDistro>?> GetDistrosAsync()
    {
        // wsl.exe writes UTF-16LE to stdout
        var result = await ProcessRunner.RunAsync("wsl", "--list --verbose", 15_000, Encoding.Unicode);
        if (result is null) return null;
        if (!result.Success) return [];
        return ParseListOutput(result.StdOut);
    }

    /// <summary>Parses `wsl -l -v` output: "  NAME  STATE  VERSION" rows, default marked with *.</summary>
    public static List<WslDistro> ParseListOutput(string output)
    {
        var distros = new List<WslDistro>();
        var lines = output.Replace("\0", "").Replace("\r", "").Split('\n');

        foreach (var raw in lines.Skip(1)) // skip header
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0) continue;

            var isDefault = line.TrimStart().StartsWith('*');
            var cleaned = line.Replace("*", " ").Trim();
            var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[^1], out var version)) continue;

            var state = parts[^2];
            var name = string.Join(' ', parts[..^2]);
            distros.Add(new WslDistro(name, state, version, isDefault));
        }

        return distros;
    }

    public static Task<ProcessResult?> TerminateAsync(string distro)
        => ProcessRunner.RunAsync("wsl", $"--terminate \"{Sanitize(distro)}\"", 15_000, Encoding.Unicode);

    /// <summary>Distro names are parsed from wsl output; strip quotes so a crafted
    /// name can never inject extra arguments into the command line.</summary>
    private static string Sanitize(string distro) => distro.Replace("\"", "");

    public static Task<ProcessResult?> ShutdownAllAsync()
        => ProcessRunner.RunAsync("wsl", "--shutdown", 15_000, Encoding.Unicode);

    /// <summary>Opens a new terminal window running the given distro.</summary>
    public static void LaunchTerminal(string distro)
    {
        var safeName = Sanitize(distro);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "wt.exe",
            Arguments = $"wsl -d \"{safeName}\"",
            UseShellExecute = true,
        };
        try
        {
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Windows Terminal not installed — fall back to a plain console window.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d \"{safeName}\"",
                UseShellExecute = true,
            });
        }
    }
}
