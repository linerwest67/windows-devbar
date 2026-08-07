using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using DevBar.Core.Vitals;
using H.NotifyIcon;

namespace DevBar;

/// <summary>
/// Owns the tray icon: renders the live CPU% text as the icon, handles clicks
/// and the right-click context menu.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly TaskbarIcon _taskbarIcon;
    private readonly App _app;
    private Icon? _currentIcon;
    private IntPtr _currentIconHandle;

    public TrayIconManager(App app, MainPopup popup)
    {
        _app = app;

        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open DevBar" };
        openItem.Click += (_, _) => popup.ShowPopup();
        menu.Items.Add(openItem);

        var stayAwakeItem = new MenuItem { Header = "Stay awake", IsCheckable = true, IsChecked = app.StayAwake.IsEnabled };
        stayAwakeItem.Click += (_, _) =>
        {
            if (stayAwakeItem.IsChecked) app.StayAwake.Enable();
            else app.StayAwake.Disable();
            app.Settings.StayAwakeEnabled = stayAwakeItem.IsChecked;
            app.Settings.Save();
        };
        menu.Items.Add(stayAwakeItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "DevBar",
            ContextMenu = menu,
        };
        _taskbarIcon.TrayLeftMouseUp += (_, _) => popup.TogglePopup();

        UpdateIcon(null);
        _taskbarIcon.ForceCreate();
    }

    public void UpdateIcon(VitalsSnapshot? vitals)
    {
        var text = vitals is null ? "--" : $"{Math.Round(vitals.CpuPercent):F0}";
        var (newIcon, newHandle) = RenderTextIcon(text);
        _taskbarIcon.Icon = newIcon;
        if (vitals is not null)
        {
            _taskbarIcon.ToolTipText =
                $"DevBar — CPU {vitals.CpuPercent:F0}% · RAM {vitals.MemoryPercent:F0}%";
        }

        ReleaseCurrentIcon();
        _currentIcon = newIcon;
        _currentIconHandle = newHandle;
    }

    /// <summary>
    /// Icon.Dispose does not free a handle obtained from Bitmap.GetHicon, so the
    /// handle must be destroyed explicitly or the app leaks one per refresh.
    /// </summary>
    private void ReleaseCurrentIcon()
    {
        _currentIcon?.Dispose();
        _currentIcon = null;
        if (_currentIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_currentIconHandle);
            _currentIconHandle = IntPtr.Zero;
        }
    }

    public void ShowNotification(string title, string message)
        => _taskbarIcon.ShowNotification(title, message);

    private static (Icon Icon, IntPtr Handle) RenderTextIcon(string text)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var font = new Font("Segoe UI", text.Length > 2 ? 13f : 16f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            var textSize = g.MeasureString(text, font);
            var x = (size - textSize.Width) / 2f;
            var y = (size - textSize.Height) / 2f;

            using var brush = new SolidBrush(Color.White);
            g.DrawString(text, font, brush, x, y);
        }

        var handle = bitmap.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }

    public void Dispose()
    {
        _taskbarIcon.Dispose();
        ReleaseCurrentIcon();
    }
}
