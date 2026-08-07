using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.StartupApps;

namespace DevBar.ViewModels;

public partial class StartupAppItemViewModel : ObservableObject
{
    public StartupApp Model { get; }
    public string Name => Model.Name;
    public string Command => Model.Command;
    public string SourceLabel => Model.Source switch
    {
        StartupSource.RunKeyCurrentUser => "HKCU Run",
        StartupSource.RunKeyLocalMachine => "HKLM Run",
        StartupSource.StartupFolder => "Startup folder",
        _ => "?",
    };

    [ObservableProperty] private bool _enabled;

    private readonly StartupAppsViewModel _parent;
    private bool _suppressToggle;

    public StartupAppItemViewModel(StartupApp model, StartupAppsViewModel parent)
    {
        Model = model;
        _parent = parent;
        _enabled = model.Enabled;
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_suppressToggle) return;
        _parent.OnItemToggled(this, value);
    }

    public void RevertWithoutSideEffect(bool value)
    {
        _suppressToggle = true;
        Enabled = value;
        _suppressToggle = false;
    }
}

public partial class StartupAppsViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<StartupAppItemViewModel> Apps { get; } = [];

    public void Refresh()
    {
        Apps.Clear();
        foreach (var app in StartupAppsService.GetStartupApps())
        {
            Apps.Add(new StartupAppItemViewModel(app, this));
        }
        StatusText = $"{Apps.Count} startup entries";
    }

    internal void OnItemToggled(StartupAppItemViewModel item, bool enabled)
    {
        if (!StartupAppsService.SetEnabled(item.Model, enabled))
        {
            StatusText = $"Could not change '{item.Name}' — HKLM entries need administrator rights";
            item.RevertWithoutSideEffect(!enabled);
        }
        else
        {
            StatusText = $"{item.Name} {(enabled ? "enabled" : "disabled")}";
        }
    }

    [RelayCommand]
    private void OpenTaskManager()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskmgr.exe", "/7 /startup")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}
