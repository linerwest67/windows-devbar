namespace DevBar.Core.Automations;

public enum TriggerKind
{
    PortOpened,
    PortClosed,
    ProcessStarted,
    ProcessStopped,
}

public enum ActionKind
{
    Notify,
    RunCommand,
    KillProcess,
}

public sealed class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";

    public TriggerKind Trigger { get; set; }

    /// <summary>Port number for port triggers; process name (without .exe) for process triggers.</summary>
    public string TriggerTarget { get; set; } = "";

    public ActionKind Action { get; set; }

    /// <summary>Command line for RunCommand; unused otherwise.</summary>
    public string ActionArgument { get; set; } = "";
}
