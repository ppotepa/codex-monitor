using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

public partial class AgentStatusWindow : Window
{
    // Avalonia's AXAML compiler and previewer require a public default
    // constructor. The desktop host uses the data-backed overload below.
    public AgentStatusWindow() : this(new AgentCatalogSnapshot(DateTimeOffset.UtcNow, [])) { }

    internal AgentStatusWindow(AgentCatalogSnapshot snapshot)
    {
        AvaloniaXamlLoader.Load(this);
        var model = new AgentStatusViewModel(snapshot);
        model.CloseRequested += Close;
        DataContext = model;
    }
}
