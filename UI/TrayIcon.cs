namespace CodexMonitor;

internal static class TrayIcon
{
    public static Icon Create(double? percent, bool hasError)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var color = hasError ? Color.FromArgb(245, 103, 105) : UsageColor.For(percent);
        using var background = new SolidBrush(Color.FromArgb(22, 27, 35));
        graphics.FillRectangle(background, 0, 0, 32, 32);
        using var progress = new SolidBrush(color);
        graphics.FillRectangle(progress, 0, 28, percent is null ? 32 : (int)Math.Round(Math.Clamp(percent.Value, 0, 100) * .32), 4);
        var label = percent is null ? "?" : Math.Round(percent.Value).ToString("0");
        using var font = new Font("Segoe UI", label.Length > 2 ? 15 : 20, FontStyle.Bold, GraphicsUnit.Pixel);
        using var foreground = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap };
        graphics.DrawString(label, font, foreground, new RectangleF(0, 0, 32, 27), format);
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
