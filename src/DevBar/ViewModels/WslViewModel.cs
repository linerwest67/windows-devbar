using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.Wsl;

namespace DevBar.ViewModels;

public partial class WslViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<WslDistro> Distros { get; } = [];

    public async void Refresh()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Querying wsl…";
        try
        {
            var distros = await WslService.GetDistrosAsync();
            Distros.Clear();
            if (distros is null)
            {
                StatusText = "WSL not available";
                return;
            }

            foreach (var d in distros) Distros.Add(d);
            var running = distros.Count(d => d.State.Equals("Running", StringComparison.OrdinalIgnoreCase));
            StatusText = $"{distros.Count} distro(s), {running} running";
        }
        catch (Exception ex)
        {
            StatusText = $"WSL error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenTerminal(WslDistro? distro)
    {
        if (distro is null) return;
        WslService.LaunchTerminal(distro.Name);
    }

    [RelayCommand]
    private async Task TerminateAsync(WslDistro? distro)
    {
        if (distro is null) return;
        await WslService.TerminateAsync(distro.Name);
        Refresh();
    }

    [RelayCommand]
    private async Task ShutdownAllAsync()
    {
        await WslService.ShutdownAllAsync();
        Refresh();
    }
}
