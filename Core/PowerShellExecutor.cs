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

        // Convert paths to 8.3 format to avoid issues with special characters, spaces, and OneDrive
        var scriptPathShort = PathHelper.GetShortPath(scriptPath);
        var workspaceRootShort = PathHelper.GetShortPath(ctx.WorkspaceRoot);
        var logFileShort = PathHelper.GetShortPath(ctx.LogFilePath);
        var contextJsonShort = PathHelper.GetShortPath(ctx.ContextJsonPath);

        _log.Debug($"Original script path: {scriptPath}");
        _log.Debug($"Short script path (8.3): {scriptPathShort}");
        
        // Verify short path still exists
        if (!File.Exists(scriptPathShort) && !File.Exists(scriptPath))
        {
            _log.Error($"Script not found (both original and short path): {scriptPath}");
            return new PowerShellResult { ExitCode = 2, StdErr = $"Script file not found: {scriptPath}" };
        }

        // Use short path if conversion succeeded, otherwise fall back to original
        var finalScriptPath = scriptPathShort != scriptPath && File.Exists(scriptPathShort) 
            ? scriptPathShort 
            : scriptPath;

        var ps = ResolvePowerShell();
        if (string.IsNullOrWhiteSpace(ps))
            return new PowerShellResult { ExitCode = 127, StdErr = "No PowerShell found (pwsh/powershell)." };

        // Build arguments:
        // -ExecutionPolicy Bypass deve vir antes de -File para evitar restrições de política
        // -NonInteractive garante que o PowerShell não solicite entrada do usuário
        // -File é o último argumento de engine, com o caminho do script entre aspas duplas
        // Use short paths (8.3) to avoid issues with special characters and OneDrive
        var args = new List<string>
        {
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy", "Bypass",
            "-File", Quote(finalScriptPath),
            "-Mode", Quote(mode),
            "-RunId", Quote(ctx.RunId),
            "-WorkspaceRoot", Quote(workspaceRootShort != ctx.WorkspaceRoot && Directory.Exists(workspaceRootShort) ? workspaceRootShort : ctx.WorkspaceRoot),
            "-LogFile", Quote(logFileShort != ctx.LogFilePath && File.Exists(logFileShort) ? logFileShort : ctx.LogFilePath),
            "-ContextJson", Quote(contextJsonShort != ctx.ContextJsonPath && File.Exists(contextJsonShort) ? contextJsonShort : ctx.ContextJsonPath)
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
        _log.Debug($"Original script path: {scriptPath}");
        if (finalScriptPath != scriptPath)
        {
            _log.Debug($"Using 8.3 short path: {finalScriptPath}");
        }

        using var p = new Process { StartInfo = psi };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        var outputLock = new object();
        var errorLock = new object();
        var outputComplete = false;
        var errorComplete = false;

        // Event handlers for async output reading with real-time logging
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (outputLock)
                {
                    sbOut.AppendLine(e.Data);
                    // Log output in real-time for better feedback
                    _log.Debug($"[PS Output] {e.Data}");
                }
            }
            else
            {
                // End of stream
                outputComplete = true;
            }
        };

        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (errorLock)
                {
                    sbErr.AppendLine(e.Data);
                    // Log errors in real-time
                    _log.Warn($"[PS Error] {e.Data}");
                }
            }
            else
            {
                // End of stream
                errorComplete = true;
            }
        };

        try
        {
            p.Start();
            
            // Begin async reading BEFORE waiting for exit
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // Wait for process to exit
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                _log.Error("PowerShell script execution timed out");
                return new PowerShellResult { ExitCode = 124, StdErr = "Timed out running PowerShell script." };
            }

            // Wait for async output reading to complete (with timeout)
            // After WaitForExit, we need to wait for the async readers to finish
            var waitStart = DateTime.UtcNow;
            var maxWaitMs = 5000; // Maximum 5 seconds to wait for async readers
            
            while ((!outputComplete || !errorComplete) && (DateTime.UtcNow - waitStart).TotalMilliseconds < maxWaitMs)
            {
                System.Threading.Thread.Sleep(100);
            }

            // If async reading didn't complete, log a warning but continue
            if (!outputComplete || !errorComplete)
            {
                _log.Debug($"Async reading incomplete after process exit (Output: {outputComplete}, Error: {errorComplete})");
            }

            var res = new PowerShellResult 
            { 
                ExitCode = p.ExitCode, 
                StdOut = sbOut.ToString(), 
                StdErr = sbErr.ToString() 
            };

            if (!res.Success)
            {
                _log.Warn($"Script failed: {Path.GetFileName(scriptPath)} exit={res.ExitCode}");
                if (!string.IsNullOrWhiteSpace(res.StdErr))
                {
                    _log.Warn($"PowerShell error output: {res.StdErr}");
                }
            }
            else
            {
                _log.Debug($"Script completed successfully: {scriptFileName}");
            }

            return res;
        }
        catch (Exception ex)
        {
            _log.Error($"Exception while running PowerShell script: {ex.Message}");
            _log.Error($"Stack trace: {ex.StackTrace}");
            return new PowerShellResult 
            { 
                ExitCode = -1, 
                StdErr = $"Exception: {ex.Message}" 
            };
        }
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


