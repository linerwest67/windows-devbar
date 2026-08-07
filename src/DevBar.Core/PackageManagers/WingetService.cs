using System.Text;

namespace DevBar.Core.PackageManagers;

public sealed record WingetUpgrade(string Name, string Id, string CurrentVersion, string AvailableVersion);

public static class WingetService
{
    public static async Task<bool> IsAvailableAsync()
        => await ProcessRunner.RunAsync("winget", "--version", 10_000) is { Success: true };

    public static async Task<List<WingetUpgrade>> GetUpgradesAsync()
    {
        var result = await ProcessRunner.RunAsync(
            "winget",
            "upgrade --accept-source-agreements --disable-interactivity",
            60_000,
            Encoding.UTF8);

        if (result is not { Success: true }) return [];
        return ParseUpgradeTable(result.StdOut);
    }

    /// <summary>
    /// Parses winget's fixed-width table using the header row's column offsets.
    /// Columns: Name, Id, Version, Available, Source.
    /// </summary>
    public static List<WingetUpgrade> ParseUpgradeTable(string output)
    {
        var upgrades = new List<WingetUpgrade>();
        var lines = output.Replace("\r", "").Split('\n');

        var headerIndex = Array.FindIndex(lines, l =>
            l.Contains("Name") && l.Contains("Id") && l.Contains("Version") && l.Contains("Available"));
        if (headerIndex < 0 || headerIndex + 2 >= lines.Length) return upgrades;

        var header = lines[headerIndex];
        var idCol = header.IndexOf("Id", StringComparison.Ordinal);
        var versionCol = header.IndexOf("Version", StringComparison.Ordinal);
        var availableCol = header.IndexOf("Available", StringComparison.Ordinal);
        var sourceCol = header.IndexOf("Source", StringComparison.Ordinal);
        if (idCol < 0 || versionCol < 0 || availableCol < 0) return upgrades;

        for (var i = headerIndex + 2; i < lines.Length; i++) // +2 skips the ---- separator
        {
            var line = lines[i];
            if (line.Trim().Length == 0) break;
            if (line.StartsWith("The following", StringComparison.OrdinalIgnoreCase)) break;
            if (line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.Length <= availableCol) continue;

            var name = Slice(line, 0, idCol);
            var id = Slice(line, idCol, versionCol);
            var current = Slice(line, versionCol, availableCol);
            var available = sourceCol > availableCol && line.Length > sourceCol
                ? Slice(line, availableCol, sourceCol)
                : line[availableCol..].Trim();

            if (name.Length == 0 || id.Length == 0) continue;
            upgrades.Add(new WingetUpgrade(name, id, current, available));
        }

        return upgrades;
    }

    private static string Slice(string line, int start, int end)
    {
        if (start >= line.Length) return "";
        end = Math.Min(end, line.Length);
        return line[start..end].Trim();
    }

    public static async Task<ProcessResult?> UpgradePackageAsync(string id)
    {
        // The id is parsed out of winget's own output, but strip quotes anyway so a
        // crafted package name can never break out of the quoted argument.
        var safeId = id.Replace("\"", "");
        return await ProcessRunner.RunAsync(
            "winget",
            $"upgrade --id \"{safeId}\" --silent --accept-source-agreements --accept-package-agreements --disable-interactivity",
            600_000);
    }
}
