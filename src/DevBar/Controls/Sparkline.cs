using System.Windows;
using System.Windows.Media;

namespace DevBar.Controls;

/// <summary>
/// Minimal history graph: a stroked line over a soft gradient fill, scaled to the
/// element's size. Values render left-to-right; the newest sample is rightmost.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AutoScaleProperty = DependencyProperty.Register(
        nameof(AutoScale), typeof(bool), typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Number of samples the x-axis is laid out for, so a partially
    /// filled buffer grows in from the left instead of stretching.</summary>
    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(Sparkline),
        new FrameworkPropertyMetadata(90, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>When true, the y-axis scales to the largest sample (for unbounded
    /// series like network throughput). Maximum is used as the floor.</summary>
    public bool AutoScale
    {
        get => (bool)GetValue(AutoScaleProperty);
        set => SetValue(AutoScaleProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var values = Values;
        var width = ActualWidth;
        var height = ActualHeight;
        if (values is null || values.Count < 2 || width <= 0 || height <= 0) return;

        var max = AutoScale ? Math.Max(Maximum, values.Max()) : Maximum;
        if (max <= 0) max = 1;

        var capacity = Math.Max(Capacity, values.Count);
        var stepX = width / (capacity - 1);
        var startX = width - stepX * (values.Count - 1); // newest sample pinned to the right edge

        Point At(int i)
        {
            var y = height - Math.Clamp(values[i] / max, 0, 1) * (height - 2) - 1;
            return new Point(startX + stepX * i, y);
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(At(0), isFilled: false, isClosed: false);
            for (var i = 1; i < values.Count; i++) ctx.LineTo(At(i), true, true);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(startX, height), isFilled: true, isClosed: true);
            for (var i = 0; i < values.Count; i++) ctx.LineTo(At(i), false, false);
            ctx.LineTo(new Point(width, height), false, false);
        }
        area.Freeze();

        var strokeColor = (Stroke as SolidColorBrush)?.Color ?? Colors.DodgerBlue;
        var fill = new LinearGradientBrush(
            Color.FromArgb(70, strokeColor.R, strokeColor.G, strokeColor.B),
            Color.FromArgb(0, strokeColor.R, strokeColor.G, strokeColor.B),
            90);
        fill.Freeze();

        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round }, line);
    }
}
