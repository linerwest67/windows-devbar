using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using DevBar.Core.Settings;

namespace DevBar;

/// <summary>Registers the global summon hotkey via RegisterHotKey on a message-only window.</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xDE7B;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly Action _onHotkey;
    private bool _registered;

    /// <summary>False when the combo is already claimed by another app.</summary>
    public bool IsRegistered => _registered;

    public HotkeyManager(AppSettings settings, Action onHotkey)
    {
        _onHotkey = onHotkey;
        _source = new HwndSource(new HwndSourceParameters("DevBarHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            HwndSourceHook = WndProc,
        });

        Register(settings.HotkeyModifiers, settings.HotkeyKey);
    }

    public bool Register(string modifiers, string key)
    {
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        uint mods = 0;
        foreach (var part in modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            mods |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => 0x0002u,
                "alt" => 0x0001u,
                "shift" => 0x0004u,
                "win" => 0x0008u,
                _ => 0u,
            };
        }

        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var parsedKey)) return false;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
        if (vk == 0) return false;

        _registered = RegisterHotKey(_source.Handle, HotkeyId, mods, vk);
        return _registered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _onHotkey();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered) UnregisterHotKey(_source.Handle, HotkeyId);
        _source.Dispose();
    }
}
