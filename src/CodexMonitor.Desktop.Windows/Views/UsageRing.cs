using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CodexMonitor;

internal sealed class UsageRing : Control
{
    private double? _usedPercent;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? UsedPercent
    {
        get => _usedPercent;
        set { _usedPercent = value; Invalidate(); }
    }

    public UsageRing()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(22, 27, 35);
        Size = new Size(150, 150);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var diameter = Math.Min(Width, Height) - 18;
        var bounds = new Rectangle((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
        using var track = new Pen(Color.FromArgb(52, 60, 73), 11) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var progress = new Pen(UsageColor.For(_usedPercent), 11) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(track, bounds, -90, 360);
        if (_usedPercent is > 0)
            g.DrawArc(progress, bounds, -90, (float)(Math.Clamp(_usedPercent.Value, 0, 100) * 3.6));

        var dpiScale = DeviceDpi / 96f;
        using var valueFont = new Font("Segoe UI", 20 * dpiScale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var captionFont = new Font("Segoe UI", 9 * dpiScale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var valueBrush = new SolidBrush(Color.FromArgb(245, 248, 252));
        using var captionBrush = new SolidBrush(Color.FromArgb(151, 163, 180));
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(_usedPercent is { } used ? $"{used:0}%" : "--", valueFont, valueBrush,
            new RectangleF(0, Height / 2f - 24, Width, 34), centered);
        g.DrawString("USED", captionFont, captionBrush,
            new RectangleF(0, Height / 2f + 9, Width, 22), centered);
    }
}

internal static class UsageColor
{
    public static Color For(double? percent) => percent switch
    {
        null => Color.FromArgb(131, 142, 157),
        >= 90 => Color.FromArgb(245, 103, 105),
        >= 70 => Color.FromArgb(245, 180, 90),
        _ => Color.FromArgb(75, 211, 169)
    };
}
