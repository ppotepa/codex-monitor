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
        context.DrawEllipse(null, new Pen(Brushes.DimGray, 8), center, radius, radius);
        var indicator = new Pen(new SolidColorBrush(Color.Parse("#53CFC0")), 8);
        var fraction = Math.Clamp(UsedPercent / 100.0, 0, 1);
        var segments = (int)Math.Round(fraction * 90);
        for (var i = 0; i < segments; i++)
        {
            var angleA = -Math.PI / 2 + 2 * Math.PI * i / 90;
            var angleB = -Math.PI / 2 + 2 * Math.PI * (i + 0.75) / 90;
            context.DrawLine(indicator,
                new Point(center.X + radius * Math.Cos(angleA), center.Y + radius * Math.Sin(angleA)),
                new Point(center.X + radius * Math.Cos(angleB), center.Y + radius * Math.Sin(angleB)));
        }
    }
}
