namespace CodexMonitor;

internal sealed class UsageLimitRow : Control
{
    private UsageWindow? _window;

    public UsageLimitRow()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(32, 39, 50);
        Font = new Font("Segoe UI", 10);
        ForeColor = Color.White;
    }

    public void SetWindow(UsageWindow? window)
    {
        _window = window;
        AccessibleName = window is null ? "No additional limit" : $"{window.Name} limit, {window.UsedPercent:0}% used";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int U(int value) => LogicalToDeviceUnits(value);
        using var nameFont = new Font(Font, FontStyle.Bold);
        using var muted = new SolidBrush(Color.FromArgb(155, 168, 185));
        if (_window is null)
        {
            TextRenderer.DrawText(e.Graphics, "No other limit reported", Font, ClientRectangle, muted.Color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            return;
        }
        TextRenderer.DrawText(e.Graphics, UsageDisplayText.WindowName(_window.Name), nameFont,
            new Rectangle(U(12), U(5), Width - U(112), U(20)), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, $"{_window.UsedPercent:0}% used",
            nameFont, new Rectangle(Width - U(101), U(5), U(89), U(20)),
            UsageColor.For(_window.UsedPercent), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        var track = new Rectangle(U(12), U(30), Math.Max(U(10), Width - U(24)), U(5));
        using var background = new SolidBrush(Color.FromArgb(58, 68, 81));
        using var foreground = new SolidBrush(UsageColor.For(_window.UsedPercent));
        e.Graphics.FillRectangle(background, track);
        e.Graphics.FillRectangle(foreground, new Rectangle(track.X, track.Y,
            (int)Math.Round(track.Width * Math.Clamp(_window.UsedPercent / 100, 0, 1)), track.Height));
        TextRenderer.DrawText(e.Graphics, UsageDisplayText.ResetShort(_window.ResetsAt), Font,
            new Rectangle(U(12), U(39), Width - U(24), U(20)), muted.Color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
