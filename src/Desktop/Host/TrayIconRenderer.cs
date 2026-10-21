using Avalonia.Controls;
using SkiaSharp;

namespace CodexMonitor.Desktop;

// One 32px indicator renderer for Windows/macOS/Linux. Windows wraps the
// PNG into a valid ICO container; other platforms consume PNG directly.
internal static class TrayIconRenderer
{
    public static WindowIcon Create(double? usedPercent, bool hasError)
    {
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var pen = new SKPaint
        {
            IsAntialias = true,
            StrokeWidth = 4,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(65, 76, 93)
        };
        var ring = new SKRect(4, 4, 28, 28);
        canvas.DrawArc(ring, -90, 360, false, pen);

        var percent = Math.Clamp(usedPercent ?? 0, 0, 100);
        pen.Color = hasError ? new SKColor(245, 139, 139)
            : new SKColor(83, 207, 192);
        if (usedPercent is not null)
            canvas.DrawArc(ring, -90, (float)(percent * 3.6), false, pen);

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.Default,
            TextSize = percent >= 100 ? 9 : 11,
            FakeBoldText = true
        };
        var label = usedPercent is null ? "–" : Math.Round(percent).ToString("0");
        canvas.DrawText(label, 16, 20, textPaint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();
        var bytes = OperatingSystem.IsWindows() ? WrapWindowsIco(png) : png;
        using var stream = new MemoryStream(bytes, writable: false);
        return new WindowIcon(stream);
    }

    private static byte[] WrapWindowsIco(byte[] png)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1); // ICO
            writer.Write((ushort)1); // One frame
            writer.Write((byte)32);
            writer.Write((byte)32);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(22); // ICONDIR + ICONDIRENTRY
            writer.Write(png);
        }
        return stream.ToArray();
    }
}
