using System.Text.Json;

namespace NovaisFPS.Core;

public sealed class RunContext
{
    public string RunId { get; init; } = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    
    // WorkspaceRoot: Se executado de bin/Release/net8.0-windows/, aponta para o diretório pai (raiz do projeto)
    // Se executado da raiz do projeto, usa o diretório atual
    public string WorkspaceRoot { get; init; } = GetWorkspaceRoot();
    
    public string LogsDir { get; init; } = "Logs";
    public string BackupDir { get; init; } = "Backup";
    public string ProfilesDir { get; init; } = "Profiles";
    public string ScriptsDir { get; init; } = "Scripts";
    
    private static string GetWorkspaceRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var scriptsPath = Path.Combine(baseDir, "Scripts");
        
        // Se Scripts existe no diretório base, usa o diretório base
        if (Directory.Exists(scriptsPath))
        {
            return Path.GetFullPath(baseDir);
        }
        
        // Caso contrário, tenta o diretório pai (assumindo que está em bin/Release/net8.0-windows/)
        var parentDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        var parentScriptsPath = Path.Combine(parentDir, "Scripts");
        
        if (Directory.Exists(parentScriptsPath))
        {
            return parentDir;
        }
        
        // Fallback: usa o diretório base
        return Path.GetFullPath(baseDir);
    }

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







