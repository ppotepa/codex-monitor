using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

public partial class AgentStatusWindow : Window
{
    internal AgentStatusWindow(AgentCatalogSnapshot snapshot)
    {
        AvaloniaXamlLoader.Load(this);
        var model = new AgentStatusViewModel(snapshot);
        model.CloseRequested += Close;
        DataContext = model;
    }
}
