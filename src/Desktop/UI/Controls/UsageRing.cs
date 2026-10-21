using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CodexMonitor.UI.Controls;

// Native Skia-backed renderer shared by all three desktop systems.
public sealed class UsageRing : Control
{
    public static readonly StyledProperty<double> UsedPercentProperty =
        AvaloniaProperty.Register<UsageRing, double>(nameof(UsedPercent));

    public double UsedPercent
    {
        get => GetValue(UsedPercentProperty);
        set => SetValue(UsedPercentProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UsedPercentProperty) InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 8;
        if (radius <= 0) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse("#35414F")), 8), center, radius, radius);
        var indicator = new Pen(new SolidColorBrush(Color.Parse("#53CFC0")), 8, lineCap: PenLineCap.Round);
        var fraction = Math.Clamp(UsedPercent / 100.0, 0, 1);
        if (fraction >= 1)
        {
            context.DrawEllipse(null, indicator, center, radius, radius);
            return;
        }
        if (fraction <= 0) return;
        var angle = -Math.PI / 2 + 2 * Math.PI * fraction;
        var arc = new StreamGeometry();
        using (var path = arc.Open())
        {
            path.BeginFigure(new Point(center.X, center.Y - radius), false);
            path.ArcTo(new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)),
                new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise);
            path.EndFigure(false);
        }
        context.DrawGeometry(null, indicator, arc);
    }
}
