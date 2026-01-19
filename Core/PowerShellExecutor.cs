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
        // Prefer PowerShell 7+ (pwsh). Fallback to Windows PowerShell 5.1 to avoid hard-blocking.
        if (CommandExists("pwsh")) return "pwsh";
        if (CommandExists("powershell")) return "powershell";
        return "";
    }

    public PowerShellResult RunScript(
        string scriptPath,
        string mode,
        RunContext ctx,
        Dictionary<string, string>? extraArgs = null,
        int timeoutMs = 10 * 60 * 1000)
    {
        var ps = ResolvePowerShell();
        if (string.IsNullOrWhiteSpace(ps))
            return new PowerShellResult { ExitCode = 127, StdErr = "No PowerShell found (pwsh/powershell)." };

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

        _log.Info($"Running script: {Path.GetFileName(scriptPath)} (Mode={mode}, PS={ps})");

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


