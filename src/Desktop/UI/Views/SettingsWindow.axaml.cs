using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

public partial class SettingsWindow : Window
{
    // Required by Avalonia's AXAML compiler/previewer; the desktop host uses
    // the settings-aware overload below.
    public SettingsWindow() : this(new MonitorSettings(), null, new PreviewStartupService()) { }

    internal SettingsWindow(MonitorSettings settings, IMonitorSettingsRepository? settingsRepository,
        IStartupService startup)
    {
        AvaloniaXamlLoader.Load(this);
        var model = new SettingsViewModel(settings, settingsRepository, startup);
        model.Saved += () => Close(true);
        DataContext = model;
        KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Escape) return;
            Close(false);
            e.Handled = true;
        };
    }

    private void CancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private sealed class PreviewStartupService : IStartupService
    {
        public string MenuLabel => "Start with system";
        public bool CanConfigure => false;
        public bool Enabled { get; set; }
    }
}
