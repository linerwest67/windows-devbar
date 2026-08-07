using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevBar.ViewModels;

public sealed record TabDefinition(string Key, string Icon, string Title, ObservableObject ViewModel);

public sealed record PaletteCommand(string Title, Action Execute);

public partial class MainViewModel : ObservableObject
{
    public VitalsViewModel Vitals { get; }
    public PortsViewModel Ports { get; }
    public NetworkViewModel Network { get; }
    public PackagesViewModel Packages { get; }
    public DockerViewModel Docker { get; }
    public WslViewModel Wsl { get; }
    public StartupAppsViewModel StartupApps { get; }
    public HostsViewModel Hosts { get; }
    public TimelineViewModel Timeline { get; }
    public AutomationsViewModel Automations { get; }
    public ClocksViewModel Clocks { get; }
    public ToolsViewModel Tools { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<TabDefinition> Tabs { get; } = [];

    [ObservableProperty]
    private TabDefinition? _selectedTab;

    [ObservableProperty]
    private string _searchText = "";

    public ObservableCollection<PaletteCommand> FilteredCommands { get; } = [];

    [ObservableProperty]
    private bool _isPaletteOpen;

    public MainViewModel(App app)
    {
        Vitals = new VitalsViewModel();
        Ports = new PortsViewModel(app);
        Network = new NetworkViewModel();
        Packages = new PackagesViewModel();
        Docker = new DockerViewModel(app);
        Wsl = new WslViewModel();
        StartupApps = new StartupAppsViewModel();
        Hosts = new HostsViewModel();
        Timeline = new TimelineViewModel(app.Timeline);
        Automations = new AutomationsViewModel(app.Automations);
        Clocks = new ClocksViewModel(app.Settings);
        Tools = new ToolsViewModel(app);
        Settings = new SettingsViewModel(app);

        Tabs.Add(new TabDefinition("vitals", "📊", "Vitals", Vitals));
        Tabs.Add(new TabDefinition("ports", "🔌", "Ports", Ports));
        Tabs.Add(new TabDefinition("network", "🌐", "Network", Network));
        Tabs.Add(new TabDefinition("packages", "📦", "Packages", Packages));
        Tabs.Add(new TabDefinition("docker", "🐳", "Docker", Docker));
        Tabs.Add(new TabDefinition("wsl", "🐧", "WSL", Wsl));
        Tabs.Add(new TabDefinition("startup", "🚀", "Startup", StartupApps));
        Tabs.Add(new TabDefinition("hosts", "📋", "Hosts", Hosts));
        Tabs.Add(new TabDefinition("tools", "🧰", "Tools", Tools));
        Tabs.Add(new TabDefinition("timeline", "📀", "Timeline", Timeline));
        Tabs.Add(new TabDefinition("automations", "⚙️", "Auto", Automations));
        Tabs.Add(new TabDefinition("clocks", "🕒", "Clocks", Clocks));
        Tabs.Add(new TabDefinition("settings", "⚙", "Settings", Settings));

        SelectedTab = Tabs[0];
    }

    partial void OnSelectedTabChanged(TabDefinition? value)
    {
        (value?.ViewModel as IRefreshable)?.Refresh();
    }

    partial void OnSearchTextChanged(string value)
    {
        FilteredCommands.Clear();
        var query = value.Trim();
        IsPaletteOpen = query.Length > 0;
        if (query.Length == 0) return;

        foreach (var cmd in BuildCommands())
        {
            if (cmd.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                FilteredCommands.Add(cmd);
        }
    }

    private IEnumerable<PaletteCommand> BuildCommands()
    {
        foreach (var tab in Tabs)
        {
            yield return new PaletteCommand($"Open {tab.Title}", () => SelectedTab = tab);
        }

        yield return new PaletteCommand("Toggle stay awake", () => Settings.ToggleStayAwakeCommand.Execute(null));
        yield return new PaletteCommand("Refresh current tab", () => (SelectedTab?.ViewModel as IRefreshable)?.Refresh());
        yield return new PaletteCommand("Flush DNS cache", () => Tools.FlushDnsCommand.Execute(null));
        yield return new PaletteCommand("Restart Explorer", () => Tools.RestartExplorerCommand.Execute(null));
        yield return new PaletteCommand("Export snapshot (JSON)", () => Tools.ExportSnapshotCommand.Execute(null));

        // "kill 3000" style quick actions
        var parts = SearchText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0].Equals("kill", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(parts[1], out var port))
        {
            yield return new PaletteCommand($"Kill processes on port {port}", () => Ports.KillByPortNumber(port));
        }
    }

    [RelayCommand]
    private void ExecutePaletteCommand(PaletteCommand? command)
    {
        if (command is null && FilteredCommands.Count > 0) command = FilteredCommands[0];
        command?.Execute();
        SearchText = "";
    }

    public void SelectTabByName(string key)
    {
        var tab = Tabs.FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (tab is not null) SelectedTab = tab;
    }
}

public interface IRefreshable
{
    void Refresh();
}
