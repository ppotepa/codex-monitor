using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

public partial class SettingsWindow : Window
{
    // Required by Avalonia's AXAML compiler/previewer; the desktop host uses
    // the settings-aware overload below.
    public SettingsWindow() : this(new MonitorSettings(), new PreviewStartupService()) { }

    internal SettingsWindow(MonitorSettings settings, IStartupService startup)
    {
        AvaloniaXamlLoader.Load(this);
        var model = new SettingsViewModel(settings, startup);
        model.Saved += () => Close(true);
        DataContext = model;
    }

    private sealed class PreviewStartupService : IStartupService
    {
        public string MenuLabel => "Start with system";
        public bool CanConfigure => false;
        public bool Enabled { get; set; }
    }
}
