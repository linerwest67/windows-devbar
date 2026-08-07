using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBar.Core.Automations;

namespace DevBar.ViewModels;

public partial class AutomationsViewModel : ObservableObject, IRefreshable
{
    private readonly AutomationEngine _engine;

    public ObservableCollection<AutomationRule> Rules { get; } = [];

    public static Array TriggerKinds => Enum.GetValues<TriggerKind>();
    public static Array ActionKinds => Enum.GetValues<ActionKind>();

    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private TriggerKind _newTrigger = TriggerKind.PortOpened;
    [ObservableProperty] private string _newTarget = "";
    [ObservableProperty] private ActionKind _newAction = ActionKind.Notify;
    [ObservableProperty] private string _newArgument = "";
    [ObservableProperty] private string _statusText = "";

    public AutomationsViewModel(AutomationEngine engine)
    {
        _engine = engine;
        Refresh();
    }

    public void Refresh()
    {
        Rules.Clear();
        foreach (var rule in _engine.GetRules()) Rules.Add(rule);
        StatusText = $"{Rules.Count} rule(s)";
    }

    [RelayCommand]
    private void AddRule()
    {
        var target = NewTarget.Trim();
        if (target.Length == 0)
        {
            StatusText = "Trigger target is required (a port number or process name)";
            return;
        }

        var isPortTrigger = NewTrigger is TriggerKind.PortOpened or TriggerKind.PortClosed;
        if (isPortTrigger && !ushort.TryParse(target, out _))
        {
            StatusText = "Port triggers need a numeric port (1–65535)";
            return;
        }

        var rule = new AutomationRule
        {
            Name = NewName.Trim().Length > 0 ? NewName.Trim() : $"{NewTrigger} {target}",
            Trigger = NewTrigger,
            TriggerTarget = target,
            Action = NewAction,
            ActionArgument = NewArgument.Trim(),
        };
        _engine.AddRule(rule);
        NewName = NewTarget = NewArgument = "";
        Refresh();
    }

    [RelayCommand]
    private void RemoveRule(AutomationRule? rule)
    {
        if (rule is null) return;
        _engine.RemoveRule(rule.Id);
        Refresh();
    }

    [RelayCommand]
    private void ToggleRule(AutomationRule? rule)
    {
        if (rule is null) return;
        rule.Enabled = !rule.Enabled;
        _engine.UpdateRule(rule);
        Refresh();
    }
}
