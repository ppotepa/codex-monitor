using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CodexMonitor.UI.ViewModels;

public sealed record AgentStatusItem(string Name, string State, string Source);

public sealed class AgentStatusViewModel
{
    public ObservableCollection<AgentStatusItem> Agents { get; } = [];
    public ICommand CloseCommand { get; }
    public event Action? CloseRequested;

    internal AgentStatusViewModel(AgentCatalogSnapshot snapshot)
    {
        CloseCommand = new UiCommand(() => CloseRequested?.Invoke());
        foreach (var agent in snapshot.Agents)
        {
            var status = agent.IsInstalled ? "Detected" : "Not found";
            var support = agent.IsSupported ? "Supported adapter" : "No adapter";
            Agents.Add(new AgentStatusItem(agent.Definition.DisplayName,
                $"{status} · {support}", agent.ExecutablePath ?? agent.Definition.DataSource));
        }
    }
}
