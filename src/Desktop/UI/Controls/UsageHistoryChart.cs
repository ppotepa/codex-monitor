using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Controls;

// Missing periods are displayed as gaps, never joined with fabricated data.
public sealed class UsageHistoryChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<HistorySample>> PointsProperty =
        AvaloniaProperty.Register<UsageHistoryChart, IReadOnlyList<HistorySample>>(nameof(Points), []);

    public IReadOnlyList<HistorySample> Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    private static readonly Pen GridPen = new(
        new SolidColorBrush(Color.Parse("#394658")), 1);
    private static readonly Pen HistoryPen = new(
        new SolidColorBrush(Color.Parse("#53CFC0")), 2);

    public UsageHistoryChart() { MinHeight = 72; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty) InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width - 12;
        var height = Bounds.Height - 8;
        if (width <= 4 || height <= 4) return;

        for (var i = 1; i < 4; i++)
        {
            var y = 4 + height * i / 4.0;
            context.DrawLine(GridPen, new Point(6, y), new Point(width + 6, y));
        }

        var end = DateTimeOffset.UtcNow;
        var start = end.AddHours(-24);
        HistorySample? previous = null;
        foreach (var current in Points.Where(p => p.At >= start && p.At <= end).OrderBy(p => p.At))
        {
            if (previous is not null && current.At - previous.At <= TimeSpan.FromMinutes(15))
            {
                context.DrawLine(HistoryPen, Locate(previous), Locate(current));
            }
            previous = current;
        }

        Point Locate(HistorySample sample)
        {
            var x = Math.Clamp((sample.At - start).TotalHours / 24.0, 0, 1);
            return new Point(6 + x * width,
                4 + (100 - Math.Clamp(sample.UsedPercent, 0, 100)) * height / 100.0);
        }
    }
}
