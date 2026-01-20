using System.Text.Json;

namespace NovaisFPS.Core;

public sealed class RunContext
{
    public string RunId { get; init; } = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    public string WorkspaceRoot { get; init; } = AppContext.BaseDirectory;
    public string LogsDir { get; init; } = "Logs";
    public string BackupDir { get; init; } = "Backup";
    public string ProfilesDir { get; init; } = "Profiles";
    public string ScriptsDir { get; init; } = "Scripts";

    public string LogFilePath { get; set; } = "";
    public string ContextJsonPath { get; set; } = "";

    public Dictionary<string, object> Data { get; } = new();

    public string GetAbsPath(params string[] parts)
    {
        var path = WorkspaceRoot;
        foreach (var p in parts)
            path = Path.Combine(path, p);
        return Path.GetFullPath(path);
    }

    public void SaveJson(string path)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
    }
}





