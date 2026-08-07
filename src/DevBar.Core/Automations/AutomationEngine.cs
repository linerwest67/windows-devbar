using System.Text.Json;
using DevBar.Core.Ports;
using DevBar.Core.Settings;
using DevBar.Core.Timeline;

namespace DevBar.Core.Automations;

public sealed record PortChange(bool Opened, PortInfo Port);

/// <summary>
/// Holds the rule list (persisted to JSON next to settings) and evaluates rules
/// against port/process change events produced by the port watcher.
/// </summary>
public sealed class AutomationEngine
{
    private static readonly string RulesPath = Path.Combine(AppSettings.SettingsDirectory, "automations.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _lock = new();
    private List<AutomationRule> _rules = [];
    private readonly TimelineStore _timeline;

    /// <summary>Raised when a Notify action fires; the UI shows a toast.</summary>
    public event Action<string>? NotificationRequested;

    public AutomationEngine(TimelineStore timeline)
    {
        _timeline = timeline;
        Load();
    }

    public List<AutomationRule> GetRules()
    {
        lock (_lock) return [.. _rules];
    }

    public void AddRule(AutomationRule rule)
    {
        lock (_lock)
        {
            _rules.Add(rule);
            Save();
        }
    }

    public void RemoveRule(Guid id)
    {
        lock (_lock)
        {
            _rules.RemoveAll(r => r.Id == id);
            Save();
        }
    }

    public void UpdateRule(AutomationRule rule)
    {
        lock (_lock)
        {
            var index = _rules.FindIndex(r => r.Id == rule.Id);
            if (index >= 0) _rules[index] = rule;
            Save();
        }
    }

    public void OnPortChanges(IEnumerable<PortChange> changes)
    {
        foreach (var change in changes)
        {
            _timeline.Add(TimelineCategory.Port,
                $"{(change.Opened ? "Opened" : "Closed")} {change.Port.Protocol} :{change.Port.Port} " +
                $"({change.Port.ProcessName}, pid {change.Port.Pid})");

            foreach (var rule in MatchingRules(change))
            {
                Execute(rule, change);
            }
        }
    }

    public List<AutomationRule> MatchingRules(PortChange change)
    {
        var expectedTrigger = change.Opened ? TriggerKind.PortOpened : TriggerKind.PortClosed;
        var expectedProcessTrigger = change.Opened ? TriggerKind.ProcessStarted : TriggerKind.ProcessStopped;

        lock (_lock)
        {
            return _rules.Where(r =>
                r.Enabled &&
                ((r.Trigger == expectedTrigger &&
                  int.TryParse(r.TriggerTarget, out var port) && port == change.Port.Port)
                 ||
                 (r.Trigger == expectedProcessTrigger &&
                  string.Equals(r.TriggerTarget, change.Port.ProcessName, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }
    }

    private void Execute(AutomationRule rule, PortChange change)
    {
        _timeline.Add(TimelineCategory.Automation, $"Rule '{rule.Name}' fired ({rule.Action})");

        switch (rule.Action)
        {
            case ActionKind.Notify:
                NotificationRequested?.Invoke(
                    $"{rule.Name}: {change.Port.ProcessName} {(change.Opened ? "opened" : "closed")} port {change.Port.Port}");
                break;

            case ActionKind.RunCommand when rule.ActionArgument.Length > 0:
                _ = ProcessRunner.RunAsync("cmd.exe", $"/c {rule.ActionArgument}", 60_000);
                break;

            case ActionKind.KillProcess when change.Opened:
                ProcessKiller.Kill(change.Port.Pid);
                break;
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(RulesPath))
            {
                _rules = JsonSerializer.Deserialize<List<AutomationRule>>(File.ReadAllText(RulesPath), JsonOptions) ?? [];
            }
        }
        catch
        {
            _rules = [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.SettingsDirectory);
            File.WriteAllText(RulesPath, JsonSerializer.Serialize(_rules, JsonOptions));
        }
        catch
        {
        }
    }
}
