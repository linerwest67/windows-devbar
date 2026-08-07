using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevBar.ViewModels;

namespace DevBar;

public partial class MainPopup : Window
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    public MainPopup()
    {
        InitializeComponent();
        // Realize the window handle up-front so the first ShowPopup positions correctly.
        SourceInitialized += (_, _) => Hide();
    }

    public void TogglePopup()
    {
        if (IsVisible) HidePopup();
        else ShowPopup();
    }

    public void ShowPopup()
    {
        PositionNearTray();
        Visibility = Visibility.Visible;
        Show();
        Activate();
        SearchBox.Focus();

        // Refresh whatever tab is showing so stale data isn't presented.
        if (DataContext is MainViewModel vm)
        {
            (vm.SelectedTab?.ViewModel as IRefreshable)?.Refresh();
        }
    }

    public void HidePopup()
    {
        Visibility = Visibility.Collapsed;
        Hide();
        if (DataContext is MainViewModel vm) vm.SearchText = "";
    }

    /// <summary>
    /// Anchors the popup to the corner of the work area nearest the mouse, which is
    /// where the tray icon was clicked. Keeps it fully on-screen for any taskbar edge.
    /// </summary>
    private void PositionNearTray()
    {
        var work = SystemParameters.WorkArea;
        var full = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

        GetCursorPos(out var cursor);
        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorX = cursor.X / dpi.DpiScaleX;

        // Horizontal: align near the cursor, clamped into the work area.
        var left = cursorX - Width / 2;
        left = Math.Clamp(left, work.Left + 8, work.Right - Width - 8);

        // Vertical: taskbar at the bottom (work area shorter than screen) → sit above it.
        var top = work.Bottom < full.Bottom - 1
            ? work.Bottom - Height - 8
            : work.Top + 8;
        top = Math.Clamp(top, work.Top + 8, Math.Max(work.Top + 8, work.Bottom - Height - 8));

        Left = left;
        Top = top;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (PinButton.IsChecked == true) return;
        HidePopup();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePopup();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && DataContext is MainViewModel { IsPaletteOpen: true } vm)
        {
            vm.ExecutePaletteCommandCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnPaletteItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: PaletteCommand command } &&
            DataContext is MainViewModel vm)
        {
            vm.ExecutePaletteCommandCommand.Execute(command);
        }
    }

    private void OnContentScroll(object sender, MouseWheelEventArgs e)
    {
        ContentScroller.ScrollToVerticalOffset(ContentScroller.VerticalOffset - e.Delta / 2.0);
        e.Handled = true;
    }

    private void OnTabChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: TabDefinition tab } && DataContext is MainViewModel vm)
        {
            vm.SelectedTab = tab;
        }
    }
}
