using Microsoft.Win32;

namespace DevBar.Core.StartupApps;

public enum StartupSource
{
    RunKeyCurrentUser,
    RunKeyLocalMachine,
    StartupFolder,
}

public sealed record StartupApp(string Name, string Command, StartupSource Source, bool Enabled);

/// <summary>
/// Lists startup entries from HKCU/HKLM Run keys and the user's Startup folder.
/// Enable/disable uses the same StartupApproved registry mechanism Task Manager uses,
/// so state stays in sync with Task Manager's Startup tab.
/// </summary>
public static class StartupAppsService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public static List<StartupApp> GetStartupApps()
    {
        var apps = new List<StartupApp>();

        CollectRunKey(Registry.CurrentUser, StartupSource.RunKeyCurrentUser, apps);
        CollectRunKey(Registry.LocalMachine, StartupSource.RunKeyLocalMachine, apps);
        CollectStartupFolder(apps);

        return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectRunKey(RegistryKey root, StartupSource source, List<StartupApp> apps)
    {
        try
        {
            using var key = root.OpenSubKey(RunKey);
            if (key is null) return;

            using var approved = root.OpenSubKey(ApprovedRunKey);
            foreach (var name in key.GetValueNames())
            {
                if (name.Length == 0) continue;
                var command = key.GetValue(name)?.ToString() ?? "";
                apps.Add(new StartupApp(name, command, source, IsApproved(approved, name)));
            }
        }
        catch
        {
            // No access to this hive; skip.
        }
    }

    private static void CollectStartupFolder(List<StartupApp> apps)
    {
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!Directory.Exists(folder)) return;

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedFolderKey);
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                apps.Add(new StartupApp(
                    Path.GetFileNameWithoutExtension(file), file, StartupSource.StartupFolder,
                    IsApproved(approved, fileName)));
            }
        }
        catch
        {
        }
    }

    /// <summary>Byte 0 even (usually 0x02) = enabled; odd (0x03 etc.) = disabled. Missing = enabled.</summary>
    private static bool IsApproved(RegistryKey? approvedKey, string name)
    {
        if (approvedKey?.GetValue(name) is byte[] { Length: > 0 } data)
            return (data[0] & 1) == 0;
        return true;
    }

    /// <summary>Returns false when the entry lives in HKLM and we lack write access.</summary>
    public static bool SetEnabled(StartupApp app, bool enabled)
    {
        try
        {
            var (root, subKey, valueName) = app.Source switch
            {
                StartupSource.RunKeyCurrentUser => (Registry.CurrentUser, ApprovedRunKey, app.Name),
                StartupSource.RunKeyLocalMachine => (Registry.LocalMachine, ApprovedRunKey, app.Name),
                StartupSource.StartupFolder => (Registry.CurrentUser, ApprovedFolderKey, Path.GetFileName(app.Command)),
                _ => throw new ArgumentOutOfRangeException(nameof(app)),
            };

            using var key = root.CreateSubKey(subKey);
            var data = new byte[12];
            data[0] = enabled ? (byte)0x02 : (byte)0x03;
            key.SetValue(valueName, data, RegistryValueKind.Binary);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
