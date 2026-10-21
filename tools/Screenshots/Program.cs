using CodexMonitor.Infrastructure.Windows;
using CodexMonitor.Plugins.Codex;
using System.Drawing.Imaging;
using System.Globalization;
using CodexMonitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Render real controls into bitmaps, without showing a window, accessing
        // the desktop, starting a CLI or reading any user's settings/account.
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var destination = Path.GetFullPath(args.FirstOrDefault() ?? "docs/images");
        Directory.CreateDirectory(destination);
        var checksDirectory = Path.GetFullPath("build/screenshots");
        Directory.CreateDirectory(checksDirectory);
        var info = new CodexPlugin(new WindowsExecutableLocator(), new WindowsCommandLaunchStrategy()).Info;
        var now = DateTimeOffset.Now;
        var windows = new UsageWindow[]
        {
            new("5-hour", 38, now.AddHours(2).AddMinutes(24)),
            new("weekly", 62, now.AddDays(3).AddHours(7))
        };
        var limits = new UsageSnapshot("plus", now, windows);
        var history = Enumerable.Range(0, 288).Select(i =>
        {
            var cycle = (int)Math.Floor((i - 256) / 60d);
            var step = i - 256 - cycle * 60;
            return new UsageHistoryPoint(now.AddMinutes((i - 287) * 5), "5-hour", step * 38d / 31,
                windows[0].ResetsAt!.Value.AddHours(cycle * 5).ToUnixTimeSeconds());
        }).ToArray();
        var daily = new long[] { 185_200, 312_400, 245_800, 418_600, 172_900, 355_100, 286_420 };
        var tokens = new TokenUsageSnapshot(now, 12_846_920, 842_160, 7,
            daily.Select((count, i) => new DailyTokenUsage(DateOnly.FromDateTime(now.Date).AddDays(i - 6), count)).ToArray());

        using var popup = new UsagePopup(info);
        popup.ShowSnapshot(new DashboardState(limits, windows[0], history, null, false,
            DependencyProblem.None, tokens, null, false,
            new PluginStatus(PluginHealth.Ready, LastSuccessfulAt: now)));
        using var limitsImage = Render(popup);
        limitsImage.Save(Path.Combine(destination, "limits.png"), ImageFormat.Png);
        popup.SelectView(true);
        using var tokensImage = Render(popup);
        tokensImage.Save(Path.Combine(destination, "tokens.png"), ImageFormat.Png);
        WriteOverview(limitsImage, tokensImage, Path.Combine(destination, "overview.png"));

        foreach (var state in new[] { "empty", "loading", "error" })
        {
            popup.ShowSnapshot(new DashboardState(null, null, [], null, false, DependencyProblem.None, null,
                state == "error" ? "Codex account token activity did not respond in time. Try refreshing." : null,
                state == "loading", new PluginStatus(PluginHealth.Ready, LastSuccessfulAt: now)));
            using var frame = Render(popup);
            frame.Save(Path.Combine(checksDirectory, state + ".png"), ImageFormat.Png);
        }

        popup.FitToWorkingArea(new Rectangle(0, 0, 1920, 1040));
        Assert(!popup.AutoScroll && popup.Height == 654, "1080p uses full readable layout");
        popup.FitToWorkingArea(new Rectangle(0, 0, 1280, 600));
        var constrainedSize = popup.Size;
        Assert(popup.AutoScroll && popup.Height <= 584, "Short displays scroll instead of clipping");
        popup.SelectView(false);
        popup.SelectView(true);
        Assert(popup.Size == constrainedSize, "Switching tabs retains screen constraints");
        popup.FitToWorkingArea(new Rectangle(0, 0, 1920, 1040));
        Assert(!popup.AutoScroll && popup.Height == 654, "Larger display restores full size");

        using var limitsOnly = new UsagePopup(info with { SupportsTokens = false, CanRepair = false });
        limitsOnly.SelectView(true);
        Assert(!limitsOnly.ShowingTokens, "Unsupported token capability cannot be opened");

        Console.WriteLine($"Screenshots rendered to {destination}. UI checks passed.");
    }

    private static Bitmap Render(UsagePopup popup)
    {
        popup.CreateControl();
        CreateHandles(popup);
        popup.PerformLayout();
        var bitmap = new Bitmap(popup.Width, popup.Height, PixelFormat.Format32bppArgb);
        popup.DrawToBitmap(bitmap, new Rectangle(Point.Empty, popup.Size));
        return bitmap;
    }

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }

    private static void WriteOverview(Bitmap limits, Bitmap tokens, string path)
    {
        using var image = new Bitmap(limits.Width + tokens.Width + 88, Math.Max(limits.Height, tokens.Height) + 114);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.FromArgb(13, 18, 25));
        using var heading = new Font("Segoe UI", 11, FontStyle.Bold);
        using var caption = new Font("Segoe UI", 10);
        using var foreground = new SolidBrush(Color.FromArgb(183, 198, 216));
        graphics.DrawString("LIMITS AT A GLANCE", heading, foreground, 30, 20);
        graphics.DrawString("ACCOUNT TOKEN ACTIVITY", heading, foreground, limits.Width + 58, 20);
        graphics.DrawImageUnscaled(limits, 30, 52);
        graphics.DrawImageUnscaled(tokens, limits.Width + 58, 52);
        graphics.DrawString("Windows only  /  Codex plugin  /  Illustrative data rendered from the app", caption, foreground, 30, image.Height - 36);
        image.Save(path, ImageFormat.Png);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
