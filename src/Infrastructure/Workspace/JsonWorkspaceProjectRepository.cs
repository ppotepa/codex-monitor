using System.Text.Json;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

public sealed class JsonWorkspaceProjectRepository(string filePath) : IWorkspaceProjectRepository
{
    private readonly object _gate = new();

    public IReadOnlyList<string> List()
    {
        lock (_gate) return Read().ToArray();
    }

    public void Add(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(fullPath);
        lock (_gate)
        {
            var paths = Read();
            if (paths.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) return;
            paths.Add(fullPath);
            Write(paths);
        }
    }

    public void Remove(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        lock (_gate)
        {
            var paths = Read();
            paths.RemoveAll(value => StringComparer.OrdinalIgnoreCase.Equals(value, fullPath));
            Write(paths);
        }
    }

    private List<string> Read()
    {
        if (!File.Exists(filePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(filePath)) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Workspace registry contains invalid JSON.", ex);
        }
    }

    private void Write(List<string> paths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = filePath + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
