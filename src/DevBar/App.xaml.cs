using System.Windows;
using DevBar.Core.Automations;
using DevBar.Core.Ipc;
using DevBar.Core.Ports;
using DevBar.Core.PowerManagement;
using DevBar.Core.Settings;
using DevBar.Core.Timeline;
using DevBar.Core.Vitals;
using DevBar.ViewModels;
using Microsoft.Win32;

namespace DevBar;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private PipeServer? _pipeServer;
    private TrayIconManager? _tray;
    private HotkeyManager? _hotkey;
    private MainPopup? _popup;
    private MainViewModel? _mainVm;
    private System.Windows.Threading.DispatcherTimer? _timer;

    public AppSettings Settings { get; private set; } = null!;
    public TimelineStore Timeline { get; } = new();
    private string? _lastWeather;
    public AutomationEngine Automations { get; private set; } = null!;
    public StayAwakeService StayAwake { get; } = new();
    private SystemSampler? _sampler;
    private readonly PortWatcher _portWatcher = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var deepLinkRequest = e.Args.Length > 0 ? DeepLink.ToPipeRequest(e.Args[0]) : null;

        _singleInstanceMutex = new Mutex(true, "DevBar-SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Forward to the running instance and exit.
            await PipeClient.SendAsync(deepLinkRequest ?? $"{IpcProtocol.VerbOpenTab} vitals");
            Shutdown();
            return;
        }

        RegisterUriScheme();

        Settings = AppSettings.Load();
        Automations = new AutomationEngine(Timeline);
        _sampler = new SystemSampler();

        _mainVm = new MainViewModel(this);
        _popup = new MainPopup { DataContext = _mainVm };

        _tray = new TrayIconManager(this, _popup);
        _hotkey = new HotkeyManager(Settings, () => _popup.TogglePopup());
        _mainVm.Settings.ReportHotkeyState(_hotkey.IsRegistered);

        Automations.NotificationRequested += message =>
            Dispatcher.Invoke(() => _tray?.ShowNotification("DevBar automation", message));

        if (Settings.StayAwakeEnabled) StayAwake.Enable();

        _pipeServer = new PipeServer(HandleIpcRequestAsync);
        _pipeServer.Start();

        Timeline.Add(TimelineCategory.System, "DevBar started");

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Settings.RefreshIntervalMs),
        };
        _timer.Tick += (_, _) => OnSampleTick();
        _timer.Start();
        OnSampleTick();

        if (deepLinkRequest is not null) await HandleIpcRequestAsync(deepLinkRequest);
    }

    public Core.Vitals.VitalsSnapshot? SampleVitals() => _sampler?.Sample();

    public void SetRefreshInterval(int intervalMs)
    {
        if (_timer is not null) _timer.Interval = TimeSpan.FromMilliseconds(intervalMs);
    }

    private void OnSampleTick()
    {
        if (_sampler is null || _mainVm is null) return;

        // Sample off the UI thread; apply results back on it.
        Task.Run(() =>
        {
            try
            {
                var vitals = _sampler.Sample();
                var ports = PortScanner.GetListeningPorts();
                var changes = _portWatcher.Update(ports);
                Automations.OnPortChanges(changes);

                Dispatcher.BeginInvoke(() =>
                {
                    _mainVm.Vitals.Apply(vitals);
                    _mainVm.Ports.Apply(ports);
                    _tray?.UpdateIcon(vitals);

                    var weather = _mainVm.Vitals.WeatherLabel;
                    if (_lastWeather is not null && weather != _lastWeather)
                        Timeline.Add(TimelineCategory.System, $"Machine weather: {_lastWeather} → {weather}");
                    _lastWeather = weather;
                });
            }
            catch (Exception ex)
            {
                // Without this the sampler would fail silently and the UI would
                // freeze on stale data with no indication why.
                Timeline.Add(TimelineCategory.System, $"Sampler error: {ex.Message}");
            }
        });
    }

    private async Task<string> HandleIpcRequestAsync(string request)
    {
        var parts = request.Split(' ', 2);
        var verb = parts[0].ToUpperInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : "";

        switch (verb)
        {
            case IpcProtocol.VerbPing:
                return "PONG";

            case IpcProtocol.VerbVitals:
            {
                var v = _sampler?.Sample();
                if (v is null) return "unavailable";
                return $"CPU {v.CpuPercent:F0}%  RAM {v.MemoryPercent:F0}% " +
                       $"({v.MemoryUsedBytes / 1073741824.0:F1}/{v.MemoryTotalBytes / 1073741824.0:F1} GiB)  " +
                       $"Up {v.Uptime.Days}d{v.Uptime.Hours}h{v.Uptime.Minutes}m";
            }

            case IpcProtocol.VerbPorts:
            {
                var ports = PortScanner.GetListeningPorts();
                return string.Join('\n', ports.Select(p =>
                    $"{p.Protocol,-4} {p.LocalAddress,-15} :{p.Port,-6} {p.ProcessName} ({p.Pid})"));
            }

            case IpcProtocol.VerbKillPort when int.TryParse(arg, out var port):
                return KillPortAndReport(port, "CLI");

            case IpcProtocol.VerbKillPortAsk when int.TryParse(arg, out var port):
            {
                // Deep links (devbar://kill/N) can be triggered by any webpage, so
                // never kill without the user explicitly confirming in a dialog.
                var listeners = PortScanner.GetListeningPorts().Where(p => p.Port == port).ToList();
                if (listeners.Count == 0) return $"nothing listening on :{port}";

                var processList = string.Join("\n", listeners.Select(p => $"  {p.ProcessName} (pid {p.Pid})"));
                var confirmed = await Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(
                        $"A devbar:// link asked to kill everything listening on port {port}:\n\n{processList}\n\nKill these processes?",
                        "DevBar",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No) == MessageBoxResult.Yes);

                if (!confirmed) return "cancelled by user";
                return KillPortAndReport(port, "Deep link");
            }

            case IpcProtocol.VerbExport:
            {
                var vitals = _sampler?.Sample();
                var ports = PortScanner.GetListeningPorts();
                var network = Core.Network.NetworkInfo.GetSnapshot(publicIp: null);
                return Core.Export.SnapshotExporter.ToJson(vitals, ports, network);
            }

            case IpcProtocol.VerbWslList:
            {
                var distros = await Core.Wsl.WslService.GetDistrosAsync();
                if (distros is null) return "wsl not available";
                return string.Join('\n', distros.Select(d =>
                    $"{(d.IsDefault ? "*" : " ")} {d.Name,-20} {d.State,-10} v{d.Version}"));
            }

            case IpcProtocol.VerbOpenTab:
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    _mainVm?.SelectTabByName(arg.Length > 0 ? arg : "vitals");
                    _popup?.ShowPopup();
                });
                return "ok";
            }

            default:
                return $"unknown verb: {verb}";
        }
    }

    private string KillPortAndReport(int port, string origin)
    {
        var results = ProcessKiller.KillByPort(port);
        if (results.Count == 0) return $"nothing listening on :{port}";
        Timeline.Add(TimelineCategory.Process,
            $"{origin} kill :{port} → {string.Join(", ", results.Select(r => $"{r.ProcessName}:{r.Result}"))}");
        return string.Join('\n', results.Select(r => $"{r.ProcessName} (pid {r.Pid}): {r.Result}"));
    }

    private static void RegisterUriScheme()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is null) return;

            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{DeepLink.Scheme}");
            key.SetValue("", "URL:DevBar Protocol");
            key.SetValue("URL Protocol", "");
            using var command = key.CreateSubKey(@"shell\open\command");
            command.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // Non-fatal: deep links just won't work.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StayAwake.Disable();
        _pipeServer?.Dispose();
        _hotkey?.Dispose();
        _tray?.Dispose();
        _sampler?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
