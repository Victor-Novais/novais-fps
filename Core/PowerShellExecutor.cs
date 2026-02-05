using System.Diagnostics;
using System.Text;

namespace NovaisFPS.Core;

public sealed class PowerShellResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public bool Success => ExitCode == 0;
}

public sealed class PowerShellExecutor
{
    private readonly Logger _log;

    public PowerShellExecutor(Logger log)
    {
        _log = log;
    }

    public string ResolvePowerShell()
    {
        // 1) Prioriza Windows PowerShell 64-bit em System32 (evita WoW64 redirection)
        try
        {
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            var winPs = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPs))
            {
                _log.Debug($"Using 64-bit PowerShell: {winPs}");
                return winPs;
            }
        }
        catch (Exception ex)
        {
            _log.Debug($"Failed to resolve System32 PowerShell: {ex.Message}");
        }

        // 2) Tenta PowerShell 7+ (pwsh), se disponível
        if (CommandExists("pwsh"))
        {
            _log.Debug("Using PowerShell 7+ (pwsh)");
            return "pwsh";
        }

        // 3) Por fim, usa "powershell" via PATH
        if (CommandExists("powershell"))
        {
            _log.Debug("Using PowerShell via PATH");
            return "powershell";
        }

        return "";
    }

    public PowerShellResult RunScript(
        string scriptFileName,
        string mode,
        RunContext ctx,
        Dictionary<string, string>? extraArgs = null,
        int timeoutMs = 10 * 60 * 1000)
    {
        // Constrói o caminho completo do script usando o WorkspaceRoot e ScriptsDir
        var scriptPath = Path.Combine(ctx.WorkspaceRoot, ctx.ScriptsDir, scriptFileName);
        
        // Validate script path exists
        if (!File.Exists(scriptPath))
        {
            _log.Error($"Script not found: {scriptPath}");
            _log.Error($"WorkspaceRoot: {ctx.WorkspaceRoot}, ScriptsDir: {ctx.ScriptsDir}, FileName: {scriptFileName}");
            return new PowerShellResult { ExitCode = 2, StdErr = $"Script file not found: {scriptPath}" };
        }

        var ps = ResolvePowerShell();
        if (string.IsNullOrWhiteSpace(ps))
            return new PowerShellResult { ExitCode = 127, StdErr = "No PowerShell found (pwsh/powershell)." };

        // Build arguments:
        // -ExecutionPolicy Bypass deve vir antes de -File para evitar restrições de política
        // -File é o último argumento de engine, com o caminho do script entre aspas duplas
        var args = new List<string>
        {
            "-NoLogo",
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", Quote(scriptPath),
            "-Mode", Quote(mode),
            "-RunId", Quote(ctx.RunId),
            "-WorkspaceRoot", Quote(ctx.WorkspaceRoot),
            "-LogFile", Quote(ctx.LogFilePath),
            "-ContextJson", Quote(ctx.ContextJsonPath)
        };

        if (extraArgs != null)
        {
            foreach (var kv in extraArgs)
            {
                args.Add(kv.Key);
                args.Add(Quote(kv.Value));
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = ps,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var fullCmdLine = $"{ps} {string.Join(" ", args)}";
        _log.Info($"Running script: {scriptFileName} (Mode={mode}, PS={ps})");
        _log.Debug($"Full command: {fullCmdLine}");
        _log.Debug($"Script path: {scriptPath}");

        using var p = new Process { StartInfo = psi };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();

        p.OutputDataReceived += (_, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new PowerShellResult { ExitCode = 124, StdErr = "Timed out running PowerShell script." };
        }

        var res = new PowerShellResult { ExitCode = p.ExitCode, StdOut = sbOut.ToString(), StdErr = sbErr.ToString() };
        if (!res.Success)
            _log.Warn($"Script failed: {Path.GetFileName(scriptPath)} exit={res.ExitCode}");
        return res;
    }

    private static string Quote(string s)
        => "\"" + s.Replace("\"", "\\\"") + "\"";

    private static bool CommandExists(string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = name,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(2000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}


