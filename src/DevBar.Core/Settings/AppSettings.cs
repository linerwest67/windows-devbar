using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevBar.Core.Settings;

public sealed class AppSettings
{
    public int RefreshIntervalMs { get; set; } = 2000;
    public bool LaunchAtLogin { get; set; }
    public bool StayAwakeEnabled { get; set; }
    public string HotkeyModifiers { get; set; } = "Ctrl+Alt";
    public string HotkeyKey { get; set; } = "D";

    /// <summary>Windows time zone IDs (TimeZoneInfo.Id), or the sentinel "Local".</summary>
    public List<string> ClockTimeZones { get; set; } = ["Local", "UTC"];

    public static string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevBar");

    public static string SettingsPath { get; } = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Corrupt or unreadable settings file: fall back to defaults rather than crashing startup.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
