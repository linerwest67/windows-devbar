using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DevBar;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is true;
        if (Invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value?.ToString() ?? "";
        var app = Application.Current;
        if (app is null) return Brushes.Transparent;
        return state.ToLowerInvariant() switch
        {
            "running" => app.FindResource("OkBrush"),
            "exited" or "stopped" => app.FindResource("TextDimBrush"),
            "paused" => app.FindResource("WarnBrush"),
            _ => app.FindResource("TextDimBrush"),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BytesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var bytes = value switch
        {
            long l => l,
            double d => (long)d,
            int i => i,
            _ => 0L,
        };
        return ViewModels.Format.Bytes(bytes);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
