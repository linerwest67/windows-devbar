using System.Diagnostics;
using System.Text;

namespace DevBar.Core.HostsFile;

public sealed record HostsEntry(string IpAddress, string HostName, bool Enabled, string? Comment);

/// <summary>
/// Reads and edits the Windows hosts file. Reading needs no elevation; writing does,
/// so edits are applied by relaunching a helper command elevated via UAC.
/// </summary>
public static class HostsFileService
{
    public static string HostsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    public static List<HostsEntry> Read() => Parse(File.ReadAllLines(HostsPath));

    /// <summary>
    /// Accepts only characters that cannot break the hosts-file format. Whitespace,
    /// '#', and newlines in a hostname would let one UI field write arbitrary
    /// extra entries into the (elevated) hosts file.
    /// </summary>
    public static bool IsValidHostName(string host) =>
        host.Length is > 0 and <= 255 &&
        !host.StartsWith('-') &&
        host.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    public static List<HostsEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<HostsEntry>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var enabled = !line.StartsWith('#');
            if (!enabled)
            {
                line = line.TrimStart('#').Trim();
                // Pure comments (not commented-out entries) are skipped:
                // a commented-out entry must still look like "ip host".
            }

            string? comment = null;
            var hashIndex = line.IndexOf('#');
            if (hashIndex >= 0)
            {
                comment = line[(hashIndex + 1)..].Trim();
                line = line[..hashIndex].Trim();
            }

            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!System.Net.IPAddress.TryParse(parts[0], out _)) continue;

            foreach (var host in parts.Skip(1))
            {
                entries.Add(new HostsEntry(parts[0], host, enabled, comment));
            }
        }
        return entries;
    }

    public static string Render(IEnumerable<HostsEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Managed in part by DevBar — comments outside entries are not preserved.");
        foreach (var entry in entries)
        {
            // Defense in depth: never let a field smuggle a line break into the file.
            var ip = StripLineBreaks(entry.IpAddress);
            var host = StripLineBreaks(entry.HostName);

            if (!entry.Enabled) sb.Append("# ");
            sb.Append(ip).Append('\t').Append(host);
            if (!string.IsNullOrEmpty(entry.Comment)) sb.Append("\t# ").Append(StripLineBreaks(entry.Comment));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string StripLineBreaks(string value)
        => value.Replace("\r", "").Replace("\n", "");

    /// <summary>
    /// Writes the new hosts content by launching an elevated cmd copy (UAC prompt).
    /// Returns false if the user declined elevation.
    /// </summary>
    public static bool WriteElevated(IEnumerable<HostsEntry> entries)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"devbar-hosts-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempFile, Render(entries));

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c copy /y \"{tempFile}\" \"{HostsPath}\" && del \"{tempFile}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit(15_000);
            return process?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled the UAC prompt.
            try { File.Delete(tempFile); } catch { }
            return false;
        }
    }
}
