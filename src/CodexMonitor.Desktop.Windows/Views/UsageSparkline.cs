using System.Drawing.Drawing2D;

namespace CodexMonitor;

internal sealed class UsageSparkline : Control
{
    private IReadOnlyList<UsageHistoryPoint> _points = [];

    public void SetPoints(IReadOnlyList<UsageHistoryPoint> points)
    {
        _points = points;
        Invalidate();
    }

    public UsageSparkline()
    {
        DoubleBuffered = true;
        ForeColor = Color.FromArgb(83, 207, 192);
        BackColor = Color.FromArgb(32, 39, 50);
        Font = new Font("Segoe UI", 9);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var plot = new Rectangle(34, 8, Math.Max(1, Width - 44), Math.Max(1, Height - 25));
        using var grid = new Pen(Color.FromArgb(59, 70, 82), 1);
        g.DrawLine(grid, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        g.DrawLine(grid, plot.Left, plot.Top + plot.Height / 2, plot.Right, plot.Top + plot.Height / 2);
        TextRenderer.DrawText(g, "100%", Font, new Rectangle(0, plot.Top - 6, 31, 16), Color.FromArgb(155, 168, 185), TextFormatFlags.Right | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "0%", Font, new Rectangle(0, plot.Bottom - 9, 31, 16), Color.FromArgb(155, 168, 185), TextFormatFlags.Right | TextFormatFlags.NoPadding);
        if (_points.Count < 2)
        {
            TextRenderer.DrawText(g, "Collecting history...", Font, plot, Color.FromArgb(155, 168, 185),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var minTime = DateTimeOffset.UtcNow.AddHours(-24);
        var maxTime = DateTimeOffset.UtcNow;
        float X(UsageHistoryPoint p) => plot.Left + (float)Math.Clamp((p.At - minTime).TotalSeconds / (maxTime - minTime).TotalSeconds, 0, 1) * plot.Width;
        float Y(UsageHistoryPoint p) => plot.Bottom - (float)Math.Clamp(p.UsedPercent / 100, 0, 1) * plot.Height;
        using var line = new Pen(ForeColor, 2.5f);
        for (var i = 1; i < _points.Count; i++)
        {
            if (_points[i].ResetAt != _points[i - 1].ResetAt || _points[i].At - _points[i - 1].At > TimeSpan.FromMinutes(15)) continue;
            g.DrawLine(line, X(_points[i - 1]), Y(_points[i - 1]), X(_points[i]), Y(_points[i]));
        }
        var last = _points[^1];
        using var dot = new SolidBrush(ForeColor);
        g.FillEllipse(dot, X(last) - 3.5f, Y(last) - 3.5f, 7, 7);
        TextRenderer.DrawText(g, "24h ago", Font, new Rectangle(34, Height - 18, 100, 18), Color.FromArgb(155, 168, 185));
        TextRenderer.DrawText(g, "now", Font, new Rectangle(Width - 60, Height - 18, 50, 18), Color.FromArgb(155, 168, 185), TextFormatFlags.Right);
    }
}
