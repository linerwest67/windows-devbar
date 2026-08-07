using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.PackageManagers;

namespace DevBar.ViewModels;

public partial class PackagesViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<WingetUpgrade> Upgrades { get; } = [];

    public async void Refresh()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Checking winget…";
        try
        {
            var upgrades = await WingetService.GetUpgradesAsync();
            Upgrades.Clear();
            foreach (var u in upgrades) Upgrades.Add(u);
            StatusText = upgrades.Count == 0 ? "Everything up to date" : $"{upgrades.Count} upgrade(s) available";
        }
        catch (Exception ex)
        {
            StatusText = $"winget error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpgradeAsync(WingetUpgrade? package)
    {
        if (package is null || IsBusy) return;
        IsBusy = true;
        StatusText = $"Upgrading {package.Name}…";
        try
        {
            var result = await WingetService.UpgradePackageAsync(package.Id);
            StatusText = result is { Success: true }
                ? $"{package.Name} upgraded"
                : $"{package.Name} upgrade failed (exit {result?.ExitCode.ToString() ?? "n/a"})";
            if (result is { Success: true }) Upgrades.Remove(package);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
