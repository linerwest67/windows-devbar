using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace DevBar.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DevBar";

    private readonly App _app;

    [ObservableProperty] private int _refreshIntervalMs;
    [ObservableProperty] private bool _launchAtLogin;
    [ObservableProperty] private bool _stayAwake;
    [ObservableProperty] private string _hotkeyText = "";
    [ObservableProperty] private string _statusText = "";

    public SettingsViewModel(App app)
    {
        _app = app;
        _refreshIntervalMs = app.Settings.RefreshIntervalMs;
        _launchAtLogin = IsLaunchAtLoginRegistered();
        _stayAwake = app.Settings.StayAwakeEnabled;
        _hotkeyText = $"{app.Settings.HotkeyModifiers}+{app.Settings.HotkeyKey}";
    }

    /// <summary>Called once the hotkey has been registered so a conflict is visible.</summary>
    public void ReportHotkeyState(bool registered)
    {
        if (!registered)
        {
            HotkeyText = $"{_app.Settings.HotkeyModifiers}+{_app.Settings.HotkeyKey} — unavailable";
            StatusText = "Another app already owns that hotkey. Edit HotkeyModifiers/HotkeyKey in settings.json and restart.";
        }
    }

    partial void OnRefreshIntervalMsChanged(int value)
    {
        if (value < 500) return;
        _app.Settings.RefreshIntervalMs = value;
        _app.Settings.Save();
        _app.SetRefreshInterval(value);
        StatusText = $"Refresh interval set to {value} ms";
    }

    partial void OnLaunchAtLoginChanged(bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
            {
                var exe = Environment.ProcessPath;
                if (exe is null) return;
                key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
            _app.Settings.LaunchAtLogin = value;
            _app.Settings.Save();
            StatusText = value ? "DevBar will start at login" : "Launch at login disabled";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not update launch at login: {ex.Message}";
        }
    }

    partial void OnStayAwakeChanged(bool value)
    {
        if (value) _app.StayAwake.Enable();
        else _app.StayAwake.Disable();
        _app.Settings.StayAwakeEnabled = value;
        _app.Settings.Save();
        StatusText = value ? "System will stay awake" : "Normal sleep behavior restored";
    }

    [RelayCommand]
    public void ToggleStayAwake() => StayAwake = !StayAwake;

    private static bool IsLaunchAtLoginRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        try
        {
            // The folder only exists after the first save — create it so Explorer
            // doesn't show "location is not available" on a fresh install.
            System.IO.Directory.CreateDirectory(Core.Settings.AppSettings.SettingsDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",
                Core.Settings.AppSettings.SettingsDirectory)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}
