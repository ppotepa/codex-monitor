using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

public partial class SettingsWindow : Window
{
    internal SettingsWindow(MonitorSettings settings, IStartupService startup)
    {
        AvaloniaXamlLoader.Load(this);
        var model = new SettingsViewModel(settings, startup);
        model.Saved += () => Close(true);
        DataContext = model;
    }
}
