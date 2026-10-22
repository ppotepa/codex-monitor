using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CodexMonitor.Core.Workspaces;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

public partial class WorkspaceWindow : Window
{
    private readonly WorkspaceViewModel _model = null!;
    private DashboardState? _dashboard;
    private ProviderInfo? _provider;
    private bool _disposed;

    // Avalonia's previewer requires a public parameterless constructor.
    public WorkspaceWindow() => AvaloniaXamlLoader.Load(this);

    public WorkspaceWindow(WorkspaceViewModel model)
    {
        _model = model;
        AvaloniaXamlLoader.Load(this);
        DataContext = model;
        model.PropertyChanged += (_, _) => UpdateContextControls();
        var scope = this.FindControl<ComboBox>("ScopeSelector")!;
        scope.SelectionChanged += (_, _) => UpdateContextControls();
        _model.Page = "Overview";
        RenderPage();
        UpdateContextControls();
        Closing += (_, e) =>
        {
            if (_disposed) return;
            e.Cancel = true;
            Hide();
        };
    }

    public void ApplyDashboardState(DashboardState state, ProviderInfo provider)
    {
        _dashboard = state;
        _provider = provider;
        if (_model.Page == "Overview") RenderPage();
    }

    public void ShowNearCursor()
    {
        if (!IsVisible) Show();
        Activate();
    }

    private async void NavigateClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string page }) return;
        _model.Page = page;
        this.FindControl<TextBlock>("PageTitle")!.Text = page;
        RenderPage();
        if (page == "Git Impact")
        {
            await _model.RefreshGitImpactAsync();
            if (_model.Page == page) RenderPage();
        }
    }

    private void UpdateContextControls()
    {
        if (this.FindControl<ComboBox>("ScopeSelector") is not { } scope) return;
        var selectedScope = scope.SelectedItem?.ToString();
        this.FindControl<ComboBox>("AgentSelector")!.IsVisible = selectedScope == nameof(WorkspaceScope.Agent);
        this.FindControl<ComboBox>("ProjectSelector")!.IsVisible = selectedScope == nameof(WorkspaceScope.Project);
    }

    private async void ApplyContextClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var preview = _model.PreviewContext();
            if (!await ConfirmAsync("Review workspace context", preview, "Apply context")) return;
            _model.ApplyContext();
            RenderPage();
            if (_model.Page == "Git Impact")
            {
                await _model.RefreshGitImpactAsync();
                if (_model.Page == "Git Impact") RenderPage();
            }
        }
        catch (Exception ex) { await MessageAsync("Context not changed", ex.Message); }
    }

    private async void AddProjectClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose a project or Git worktree",
                AllowMultiple = false
            });
            if (folders.Count == 0) return;
            var path = folders[0].TryGetLocalPath();
            if (path is null) throw new InvalidOperationException("The selected folder does not have a local path.");
            var preview = _model.PreviewAddProject(path);
            if (!await ConfirmAsync("Review project addition", preview, "Add project")) return;
            _model.AddProject(path);
            RenderPage();
        }
        catch (Exception ex) { await MessageAsync("Project not added", ex.Message); }
    }

    private async Task RemoveProjectAsync(ProjectDescriptor project)
    {
        try
        {
            var preview = _model.PreviewRemoveProject(project.Path);
            if (!await ConfirmAsync("Review project removal", preview, "Remove from list")) return;
            _model.RemoveProject(project.Path);
            RenderPage();
        }
        catch (Exception ex) { await MessageAsync("Project not removed", ex.Message); }
    }

    private void RenderPage()
    {
        var host = this.FindControl<StackPanel>("PageContent")!;
        host.Children.Clear();
        switch (_model.Page)
        {
            case "Overview": RenderOverview(host); break;
            case "Projects": RenderProjects(host); break;
            case "Agent Tools": RenderAgents(host); break;
            case "Settings":
                host.Children.Add(Heading("Settings", "Existing monitoring settings remain available from Quick View."));
                host.Children.Add(StatusCard(AvailabilityStatus.Ready,
                    "Usage refresh, alerts, startup and agent discovery settings are available in the existing Settings window."));
                break;
            case "Project Setup":
                host.Children.Add(Heading("Project Setup", "Choose a project to make project tools available."));
                var projects = new Button { Content = "Open Projects", HorizontalAlignment = HorizontalAlignment.Left, Tag = "Projects" };
                projects.Classes.Add("primary");
                projects.Click += NavigateClicked;
                host.Children.Add(projects);
                break;
            case "Git Impact": RenderGitImpact(host); break;
            default:
                RenderPlannedTool(host, _model.Page);
                break;
        }
    }

    private void RenderOverview(StackPanel host)
    {
        host.Children.Add(Heading("Overview", "Usage at a glance with a shared project and agent context."));
        var providerName = _provider?.DisplayName ?? "Codex";
        var selected = _dashboard?.SelectedLimit;
        var status = _dashboard?.LimitsBusy == true ? "Refreshing limits…" :
            _dashboard?.Limits is null ? "No usage data" : $"{providerName} · {_dashboard.ProviderStatus.Health}";
        host.Children.Add(Card(new StackPanel { Spacing = 7, Children =
        {
            Label("USAGE LIMITS", true),
            Label(selected is null ? status : $"{selected.UsedPercent:0}% used · {100 - selected.UsedPercent:0}% remaining", false, 20),
            Label(selected?.ResetsAt is { } reset ? $"Resets {reset.ToLocalTime():g}" : status, false)
        }}));
        host.Children.Add(Card(new StackPanel { Spacing = 7, Children =
        {
            Label("WORKSPACE", true),
            Label(_model.ContextLabel, false, 17),
            Label(_model.ProjectSummary, false)
        }}));
        if (_dashboard?.PersistenceError is { Length: > 0 } error)
            host.Children.Add(StatusCard(AvailabilityStatus.Unavailable, error));
        host.Children.Add(StatusCard(AvailabilityStatus.Ready, "The compact Limits and Tokens window remains available from the tray and Quick View button."));
    }

    private void RenderProjects(StackPanel host)
    {
        host.Children.Add(Heading("Projects", "Git worktrees from one repository are grouped while each worktree keeps its own path."));
        var add = new Button { Content = "Add project folder", HorizontalAlignment = HorizontalAlignment.Left };
        add.Classes.Add("primary");
        add.Click += AddProjectClicked;
        host.Children.Add(add);
        if (_model.Groups.Count == 0)
        {
            host.Children.Add(Card(new StackPanel { Spacing = 6, Children =
            {
                Label("No projects added", true, 17),
                Label("Choose a repository or worktree folder. Project files are left untouched.")
            }}));
            return;
        }

        foreach (var group in _model.Groups)
        {
            var rows = new StackPanel { Spacing = 8 };
            foreach (var project in group.Worktrees)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
                row.Children.Add(new StackPanel { Spacing = 3, Children =
                {
                    Label(project.Name, true),
                    Label(project.Path),
                    Label(project.RepositoryRoot is null ? "Folder without Git metadata" : $"Repository root: {project.RepositoryRoot}")
                }});
                var remove = new Button { Content = "Remove", VerticalAlignment = VerticalAlignment.Center };
                remove.Click += async (_, _) => await RemoveProjectAsync(project);
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                rows.Children.Add(Card(row));
            }
            host.Children.Add(Card(new StackPanel { Spacing = 10, Children =
            {
                Label(group.Name, true, 17),
                Label(group.RepositoryRoot, false),
                rows
            }}));
        }
    }

    private static void RenderAgents(StackPanel host)
    {
        host.Children.Add(Heading("Agent Tools", "Provider availability for the current workspace."));
        host.Children.Add(StatusCard(AvailabilityStatus.Ready, "Codex · usage limits from the existing CLI adapter."));
        host.Children.Add(StatusCard(AvailabilityStatus.NotImplemented, "Claude · provider adapter is not implemented."));
        host.Children.Add(StatusCard(AvailabilityStatus.NotImplemented, "Gemini · provider adapter is not implemented."));
    }

    private void RenderGitImpact(StackPanel host)
    {
        host.Children.Add(Heading("Git Impact", "Read-only summary of the selected worktree. Nothing is staged, changed or committed."));
        if (_model.GitImpact is not { } impact)
        {
            host.Children.Add(StatusCard(AvailabilityStatus.NoData, "Choose a project context and open Git Impact to inspect it."));
            return;
        }

        host.Children.Add(StatusCard(impact.Status, impact.Error ?? impact.Summary));
        if (impact.Status != AvailabilityStatus.Ready) return;
        host.Children.Add(Card(new StackPanel { Spacing = 7, Children =
        {
            Label("SELECTED WORKTREE", true),
            Label(impact.ProjectPath, false),
            Label(impact.Branch is null ? "Branch unavailable" : $"Branch · {impact.Branch}")
        }}));
        host.Children.Add(Card(new StackPanel { Spacing = 6, Children =
        {
            Label(impact.Summary, true, 17),
            Label($"{impact.UntrackedFiles} untracked · {impact.AddedLines} lines added · {impact.RemovedLines} lines removed"),
            Label($"Read {impact.ObservedAt.ToLocalTime():g}")
        }}));
        if (impact.Files.Count == 0) return;
        host.Children.Add(Heading("Changed files", $"Showing {impact.Files.Count} file entries."));
        foreach (var file in impact.Files)
            host.Children.Add(Card(new StackPanel { Spacing = 4, Children =
            {
                Label(file.Path, true),
                Label(file.ChangeType == "??" ? "Untracked" : $"{file.ChangeType} · +{file.AddedLines} / −{file.RemovedLines} lines")
            }}));
    }

    private void RenderPlannedTool(StackPanel host, string page)
    {
        host.Children.Add(Heading(page, "This tool is part of the workspace roadmap."));
        var status = page switch
        {
            "Activity" or "Sessions" => AvailabilityStatus.PermissionRequired,
            "Attention" => AvailabilityStatus.NoData,
            _ => AvailabilityStatus.NotImplemented
        };
        var detail = page switch
        {
            "Activity" or "Sessions" => "Codex lifecycle hooks are not configured in this repository build. No events are collected.",
            "Attention" => "No workspace attention rules are active yet.",
            _ => $"{page} is not implemented in this workspace slice."
        };
        host.Children.Add(StatusCard(status, detail));
        if (page is "Project Setup" or "Goals")
        {
            var projects = new Button { Content = "Open Projects", HorizontalAlignment = HorizontalAlignment.Left };
            projects.Click += NavigateClicked;
            projects.Tag = "Projects";
            host.Children.Add(projects);
        }
    }

    private static Control Heading(string title, string description) => new StackPanel
    {
        Spacing = 5,
        Margin = new Thickness(0, 0, 0, 6),
        Children = { Label(title, true, 21), Label(description) }
    };

    private static Control StatusCard(AvailabilityStatus status, string detail) => Card(new StackPanel
    {
        Spacing = 8,
        Children =
        {
            new Border
            {
                Background = status switch
                {
                    AvailabilityStatus.Ready => new SolidColorBrush(Color.Parse("#29453F")),
                    AvailabilityStatus.PermissionRequired => new SolidColorBrush(Color.Parse("#443926")),
                    AvailabilityStatus.Unavailable => new SolidColorBrush(Color.Parse("#482D31")),
                    _ => new SolidColorBrush(Color.Parse("#2C3540"))
                },
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = Label(status.ToString(), true, 11)
            },
            Label(detail)
        }
    });

    private static Border Card(Control child)
    {
        var border = new Border { Padding = new Thickness(16), Child = child };
        border.Classes.Add("card");
        return border;
    }

    private static TextBlock Label(string text, bool emphasized = false, double size = 13) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = emphasized ? FontWeight.SemiBold : FontWeight.Normal,
        Foreground = emphasized ? new SolidColorBrush(Color.Parse("#F1F4F8")) : new SolidColorBrush(Color.Parse("#B0BCCB")),
        TextWrapping = TextWrapping.Wrap
    };

    private async Task<bool> ConfirmAsync(string title, string preview, string confirmLabel)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 680,
            Height = 500,
            MinWidth = 500,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#15191F"))
        };
        var content = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(18) };
        content.Children.Add(new ScrollViewer { Content = new TextBlock { Text = preview, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) } });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => dialog.Close(false);
        var confirm = new Button { Content = confirmLabel };
        confirm.Classes.Add("primary");
        confirm.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        Grid.SetRow(buttons, 1);
        content.Children.Add(buttons);
        dialog.Content = content;
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task MessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#15191F"))
        };
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        content.Children.Add(Label(message));
        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80 };
        ok.Click += (_, _) => dialog.Close();
        content.Children.Add(ok);
        dialog.Content = content;
        await dialog.ShowDialog(this);
    }

    public void Dispose()
    {
        _disposed = true;
        Close();
    }
}
