using System.Runtime.InteropServices;

namespace DevBar.Core.PowerManagement;

/// <summary>Prevents system sleep (and optionally display sleep) while enabled.</summary>
public sealed class StayAwakeService
{
    [Flags]
    private enum ExecutionState : uint
    {
        Continuous = 0x80000000,
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    public bool IsEnabled { get; private set; }

    public void Enable(bool keepDisplayOn = false)
    {
        var flags = ExecutionState.Continuous | ExecutionState.SystemRequired;
        if (keepDisplayOn) flags |= ExecutionState.DisplayRequired;
        SetThreadExecutionState(flags);
        IsEnabled = true;
    }

    public void Disable()
    {
        SetThreadExecutionState(ExecutionState.Continuous);
        IsEnabled = false;
    }
}
