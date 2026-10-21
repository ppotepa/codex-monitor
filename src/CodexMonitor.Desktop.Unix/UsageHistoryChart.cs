using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CodexMonitor.Desktop.Unix;

// Lightweight 24-hour sparkline. Gaps are not connected, to avoid inventing data.
internal sealed class UsageHistoryChart : Control
{
    private IReadOnlyList<UsageHistoryPoint> _points = [];
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#394658")), 1);
    private static readonly Pen LinePen = new(new SolidColorBrush(Color.Parse("#53CFC0")), 2);

    public UsageHistoryChart()
    {
        Height = 90;
        MinWidth = 160;
    }

    public void SetPoints(IReadOnlyList<UsageHistoryPoint> points)
    {
        _points = points;
        InvalidateVisual();
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
        var readings = _points.Where(p => p.At >= start && p.At <= end)
            .OrderBy(p => p.At).ToArray();

        UsageHistoryPoint? previous = null;
        foreach (var current in readings)
        {
            if (previous is not null && current.At - previous.At <= TimeSpan.FromMinutes(15))
                context.DrawLine(LinePen, Location(previous, start, width, height),
                    Location(current, start, width, height));
            previous = current;
        }

        Point Location(UsageHistoryPoint sample, DateTimeOffset origin, double w, double h)
        {
            var elapsed = (sample.At - origin).TotalHours / 24.0;
            return new Point(6 + Math.Clamp(elapsed, 0, 1) * w,
                4 + (100 - Math.Clamp(sample.UsedPercent, 0, 100)) * h / 100.0);
        }
    }
}
