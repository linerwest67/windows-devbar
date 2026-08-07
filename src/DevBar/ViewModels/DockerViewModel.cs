using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.Docker;
using DevBar.Core.Timeline;

namespace DevBar.ViewModels;

public partial class DockerViewModel : ObservableObject, IRefreshable
{
    private readonly App _app;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<DockerContainer> Containers { get; } = [];

    public DockerViewModel(App app) => _app = app;

    public async void Refresh()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Querying docker…";
        try
        {
            var containers = await DockerService.GetContainersAsync();
            Containers.Clear();
            if (containers is null)
            {
                StatusText = "Docker unavailable (not installed, or daemon not running)";
                return;
            }

            foreach (var c in containers) Containers.Add(c);
            var running = containers.Count(c => c.State == "running");
            StatusText = $"{containers.Count} container(s), {running} running";
        }
        catch (Exception ex)
        {
            StatusText = $"Docker error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartAsync(DockerContainer? c)
    {
        if (c is null) return;
        try
        {
            await DockerService.StartAsync(c.Id);
            _app.Timeline.Add(TimelineCategory.Docker, $"Started container {c.Name}");
            Refresh();
        }
        catch (Exception ex) { StatusText = $"Start failed: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task StopAsync(DockerContainer? c)
    {
        if (c is null) return;
        try
        {
            await DockerService.StopAsync(c.Id);
            _app.Timeline.Add(TimelineCategory.Docker, $"Stopped container {c.Name}");
            Refresh();
        }
        catch (Exception ex) { StatusText = $"Stop failed: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task RestartAsync(DockerContainer? c)
    {
        if (c is null) return;
        try
        {
            await DockerService.RestartAsync(c.Id);
            _app.Timeline.Add(TimelineCategory.Docker, $"Restarted container {c.Name}");
            Refresh();
        }
        catch (Exception ex) { StatusText = $"Restart failed: {ex.Message}"; }
    }
}
