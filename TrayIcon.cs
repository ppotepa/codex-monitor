using System.Drawing.Drawing2D;

namespace CodexMonitor;

internal static class TrayIcon
{
    public static Icon Create(double? percent, bool hasError)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var color = hasError ? Color.FromArgb(220, 90, 70)
            : percent is null ? Color.Gray
            : percent >= 90 ? Color.FromArgb(220, 75, 60)
            : percent >= 70 ? Color.FromArgb(230, 160, 45)
            : Color.FromArgb(40, 155, 110);
        using var background = new SolidBrush(Color.FromArgb(28, 32, 40));
        graphics.FillEllipse(background, 1, 1, 30, 30);
        using var pen = new Pen(color, 3.5f);
        graphics.DrawEllipse(pen, 2, 2, 28, 28);
        var label = percent is null ? "?" : Math.Round(percent.Value).ToString("0");
        if (label.Length > 2) label = "99+";
        using var font = new Font("Segoe UI", label.Length > 2 ? 10 : 12, FontStyle.Bold, GraphicsUnit.Pixel);
        using var foreground = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(label, font, foreground, new RectangleF(1, 1, 30, 30), format);
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
