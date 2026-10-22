using System.Text.Json;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

// Local, account-free persistence. The canonical project path is the identity.
public sealed class JsonWorkspaceGoalRepository(string filePath) : IWorkspaceGoalRepository
{
    private readonly object _gate = new();

    public WorkspaceGoal? Find(string projectPath)
    {
        lock (_gate)
            return Read().FirstOrDefault(goal => StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFullPath(goal.ProjectPath), Path.GetFullPath(projectPath)));
    }

    public void Save(WorkspaceGoal goal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(goal.Title);
        if (goal.BudgetMinutes is < 1 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(goal), "Budget must be between 1 and 100000 minutes.");
        lock (_gate)
        {
            var all = Read();
            all.RemoveAll(existing => StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFullPath(existing.ProjectPath), Path.GetFullPath(goal.ProjectPath)));
            all.Add(goal);
            var folder = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(folder);
            var temp = filePath + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    private List<WorkspaceGoal> Read()
    {
        if (!File.Exists(filePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<WorkspaceGoal>>(File.ReadAllText(filePath)) ?? [];
        }
        catch (JsonException ex)
        {
            // Never silently replace a corrupt goals file with an empty list.
            throw new InvalidDataException("Workspace goals storage contains invalid JSON.", ex);
        }
    }
}
