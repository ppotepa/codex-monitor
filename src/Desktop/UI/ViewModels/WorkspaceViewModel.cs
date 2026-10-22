using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CodexMonitor.UI.ViewModels;

// A presentation-only model. Local tools never require an account or network access.
public sealed class WorkspaceViewModel : BindableBase
{
    private string _projectPath = "";
    private string _projectStatus = "Choose a local Git repository to inspect.";
    private string _projectName = "No project selected";
    private string _gitSummary = "—";
    private string _instructions = "Select a project to read its AGENTS.md.";
    private string _skillsSummary = "Local skills are read-only in this version.";
    private string _advisorText = "Recommendations become available when limits have been read.";
    private bool _isLoading;
    private int _selectedPage;

    public DashboardViewModel Usage { get; }
    public ObservableCollection<string> ProjectFiles { get; } = [];
    public ObservableCollection<string> Skills { get; } = [];
    public string ProjectPath { get => _projectPath; set => Set(ref _projectPath, value); }
    public string ProjectStatus { get => _projectStatus; private set => Set(ref _projectStatus, value); }
    public string ProjectName { get => _projectName; private set => Set(ref _projectName, value); }
    public string GitSummary { get => _gitSummary; private set => Set(ref _gitSummary, value); }
    public string Instructions { get => _instructions; private set => Set(ref _instructions, value); }
    public string SkillsSummary { get => _skillsSummary; private set => Set(ref _skillsSummary, value); }
    public string AdvisorText { get => _advisorText; private set => Set(ref _advisorText, value); }
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public int SelectedPage { get => _selectedPage; set => Set(ref _selectedPage, value); }
    public ICommand InspectProjectCommand { get; }
    public ICommand RefreshSkillsCommand { get; }
    public ICommand ShowOverviewCommand { get; }
    public ICommand ShowProjectsCommand { get; }
    public ICommand ShowSkillsCommand { get; }
    public ICommand ShowInstructionsCommand { get; }

    public WorkspaceViewModel(DashboardViewModel usage)
    {
        Usage = usage;
        InspectProjectCommand = new UiCommand(() => _ = InspectProjectAsync());
        RefreshSkillsCommand = new UiCommand(RefreshSkills);
        ShowOverviewCommand = new UiCommand(() => SelectedPage = 0);
        ShowProjectsCommand = new UiCommand(() => SelectedPage = 1);
        ShowSkillsCommand = new UiCommand(() => SelectedPage = 3);
        ShowInstructionsCommand = new UiCommand(() => SelectedPage = 4);
        RefreshSkills();
        usage.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(DashboardViewModel.RemainingText) or nameof(DashboardViewModel.ResetText))
                RefreshAdvice();
        };
        RefreshAdvice();
    }

    private void RefreshAdvice()
    {
        AdvisorText = Usage.RemainingText == "No data"
            ? "Refresh Codex limits to see the remaining allowance. No AI calls are made automatically."
            : Usage.RemainingText + " · " + Usage.ResetText +
              ". Use remaining quota according to active work; no unobserved sessions are assumed.";
    }

    public async Task InspectProjectAsync()
    {
        if (IsLoading) return;
        var path = ProjectPath.Trim().Trim('"');
        if (!Directory.Exists(path))
        {
            ProjectStatus = "Directory not found. Enter an existing local folder.";
            return;
        }
        IsLoading = true;
        ProjectStatus = "Reading local repository…";
        try
        {
            var result = await Task.Run(() => ReadGit(path));
            ProjectName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            GitSummary = result.Summary;
            ProjectStatus = result.Status;
            ProjectFiles.Clear();
            foreach (var file in result.Files) ProjectFiles.Add(file);
            var agents = Path.Combine(path, "AGENTS.md");
            Instructions = File.Exists(agents)
                ? File.ReadAllText(agents)
                : "No root AGENTS.md found. Nested instruction files are not inspected yet.";
            RefreshSkills();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            ProjectStatus = "Local inspection failed: " + ex.Message;
        }
        finally { IsLoading = false; }
    }

    private static (string Summary, string Status, string[] Files) ReadGit(string directory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("status");
        process.StartInfo.ArgumentList.Add("--short");
        process.StartInfo.ArgumentList.Add("--untracked-files=normal");
        if (!process.Start()) throw new InvalidOperationException("Unable to start Git.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(5000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Git status timed out.");
        }
        if (process.ExitCode != 0) return ("—", "Not a readable Git repository: " + stderr.Trim(), []);
        var files = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (files.Length + " changed/untracked files", "Working tree checked locally. Not attributed to a specific agent.", files.Take(100).ToArray());
    }

    public void RefreshSkills()
    {
        Skills.Clear();
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agents", "skills"),
            Directory.Exists(ProjectPath.Trim().Trim('"')) ? Path.Combine(ProjectPath.Trim().Trim('"'), ".agents", "skills") : ""
        };
        SkillsSummary = "";
        foreach (var path in paths.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path)) continue;
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(path))
                {
                    if (File.Exists(Path.Combine(directory, "SKILL.md")))
                        Skills.Add(Path.GetFileName(directory) + "  ·  " + path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SkillsSummary = "Some skill folders could not be read: " + ex.Message;
            }
        }
        if (!SkillsSummary.StartsWith("Some skill", StringComparison.Ordinal))
            SkillsSummary = Skills.Count + " local skills found. Installing or changing skills is not enabled here.";
    }
}
