using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CodexMonitor.UI.Views;

// Shared Avalonia UI for Windows, Linux and macOS.
public partial class WorkspaceOverviewView : UserControl
{
    public WorkspaceOverviewView() => AvaloniaXamlLoader.Load(this);
}
