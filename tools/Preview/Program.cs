using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Markup.Xaml.Styling;
using CodexMonitor.UI.Views;
using CodexMonitor.UI.ViewModels;

AppBuilder.Configure<PreviewApp>().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();
var output = Path.GetFullPath(args.FirstOrDefault() ?? "build/preview");
Directory.CreateDirectory(output);
var model = new DashboardViewModel();
var window = new DashboardWindow(model, false);
var provider = new ProviderInfo("codex", "Codex", "Built-in provider", [], true, true, [], "Illustrative data");
var now = DateTimeOffset.UtcNow;
var limits = new UsageSnapshot("Plus", now, [
    new UsageWindow("5-hour", 38, now.AddHours(2.4)),
    new UsageWindow("weekly", 62, now.AddDays(3))]);
long[] counts = [185200, 312400, 245800, 418600, 172900, 355100, 286420];
var tokens = new TokenUsageSnapshot(now, 12846920, 842160, 7,
    counts.Select((count, i) => new DailyTokenUsage(DateOnly.FromDateTime(now.Date).AddDays(i - 6), count)).ToArray());
var history = Enumerable.Range(0, 289).Select(i =>
    new UsageHistoryPoint(now.AddMinutes((i - 288) * 5), "5-hour", 10 + i % 65, null, "codex")).ToArray();
var state = new DashboardState(limits, limits.MainWindow, history, null, false,
    DependencyProblem.None, tokens, null, false, new ProviderStatus(ProviderHealth.Ready));
window.SelectProvider(provider);
window.ApplyState(state);
window.Show();
Capture("limits");
window.FindControl<TabControl>("MainTabs")!.SelectedIndex = 1;
Capture("tokens");
window.ApplyState(state with { Tokens = null });
Capture("empty-tokens");
window.Width = 360;
window.Height = 420;
window.ApplyState(state with { TokenError = "Account activity could not be refreshed. Showing the last successful result." });
Capture("compact-error");
window.Dispose();
var settings = new SettingsWindow(new MonitorSettings(), null, new PreviewStartup());
settings.Show();
CaptureWindow(settings, "settings");
CaptureWindow(settings, "settings-125", 1.25);
CaptureWindow(settings, "settings-150", 1.5);
var settingsModel = (SettingsViewModel)settings.DataContext!;
settingsModel.AlertsEnabled = false;
settingsModel.AgentsInterval = 0;
CaptureWindow(settings, "settings-alerts-off");
settingsModel.AlertsEnabled = true;
settingsModel.WarningText = "95";
settingsModel.CriticalText = "80";
settingsModel.SaveCommand.Execute(null); // Invalid input: exercises validation without writing settings.
settings.Height = 480;
CaptureWindow(settings, "settings-error-compact");
settings.Close();
Console.WriteLine($"Rendered synthetic Avalonia previews to {output}");

void Capture(string name)
{
    CaptureWindow(window, name);
    if (name is "limits" or "tokens")
    {
        CaptureWindow(window, name + "-125", 1.25);
        CaptureWindow(window, name + "-150", 1.5);
    }
}

void CaptureWindow(Window target, string name, double scale = 1)
{
    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    target.UpdateLayout();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    if (scale != 1)
    {
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
            new PixelSize((int)(target.Bounds.Width * scale), (int)(target.Bounds.Height * scale)),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(target);
        bitmap.Save(Path.Combine(output, name + ".png"));
        return;
    }
    using var frame = target.CaptureRenderedFrame()
        ?? throw new InvalidOperationException("No rendered preview frame.");
    frame.Save(Path.Combine(output, name + ".png"));
}

sealed class PreviewApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new StyleInclude(new Uri("avares://CodexMonitor.UI/"))
            { Source = new Uri("avares://CodexMonitor.UI/Styles/Theme.axaml") });
    }
}

sealed class PreviewStartup : IStartupService
{
    public string MenuLabel => "Start with Windows";
    public bool CanConfigure => true;
    public bool Enabled { get; set; }
}
