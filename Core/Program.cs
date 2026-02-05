using System.Diagnostics;
using System.Text;
using NovaisFPS.Core;

if (args.Length > 0)
{
    // Lightweight helper modes invoked from PowerShell
    if (string.Equals(args[0], "--msi-enforce", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"msienforcer-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var code = MSIEnforcer.Run(helperLog);
        Environment.Exit(code);
    }

    if (string.Equals(args[0], "--memory-clean", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"memoryclean-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var code = MemoryCleaner.PurgeStandby(helperLog);
        Environment.Exit(code);
    }

    if (string.Equals(args[0], "--health", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"health-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var snap = HealthMetrics.Collect(helperLog);
        helperLog.Info($"Health: DPC={snap.DpcPercent}% ISR={snap.IsrPercent}% TimerRes current={snap.CurrentTimerResolutionMs}ms min={snap.MinTimerResolutionMs}ms max={snap.MaxTimerResolutionMs}ms");
        Environment.Exit(0);
    }
}

static void ClearAndBanner()
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.Clear();
    Console.WriteLine("███╗   ██╗ ██████╗ ██╗   ██╗ █████╗ ██╗███████╗");
    Console.WriteLine("████╗  ██║██╔═══██╗██║   ██║██╔══██╗██║██╔════╝");
    Console.WriteLine("██╔██╗ ██║██║   ██║██║   ██║███████║██║███████╗");
    Console.WriteLine("██║╚██╗██║██║   ██║╚██╗ ██╔╝██╔══██║██║╚════██║");
    Console.WriteLine("██║ ╚████║╚██████╔╝ ╚████╔╝ ██║  ██║██║███████║");
    Console.WriteLine("╚═╝  ╚═══╝ ╚═════╝   ╚═══╝  ╚═╝  ╚═╝╚═╝╚══════╝");
    Console.WriteLine();
    Console.WriteLine("        N O V A I S   F P S");
    Console.WriteLine(" Competitive Performance Optimizer");
    Console.WriteLine();
}

static bool Confirm(string prompt)
{
    Console.Write($"{prompt} (Y/N): ");
    var key = Console.ReadKey(intercept: true);
    Console.WriteLine(key.KeyChar);
    return key.Key is ConsoleKey.Y;
}

// Desbloqueio automático de scripts .ps1 na pasta Scripts (remove Zone.Identifier)
static void TryUnblockScripts(string scriptsDir, Logger log)
{
    try
    {
        if (!Directory.Exists(scriptsDir))
        {
            log.Warn($"Scripts directory not found: {scriptsDir}");
            return;
        }

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        var psPath = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");

        if (!File.Exists(psPath))
        {
            log.Warn($"PowerShell not found at {psPath}; skipping automatic unblock.");
            return;
        }

        // Escapa o caminho para uso no comando PowerShell
        var escapedPath = scriptsDir.Replace("'", "''");
        var command = $"Get-ChildItem -Path '{escapedPath}' -Filter '*.ps1' -Recurse | Unblock-File -ErrorAction SilentlyContinue";

        var psi = new ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi);
        if (p == null)
        {
            log.Warn("Failed to start PowerShell process for Unblock-File.");
            return;
        }

        if (!p.WaitForExit(30000))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            log.Warn("Timeout executing Unblock-File; some scripts may remain blocked.");
            return;
        }

        if (p.ExitCode != 0)
        {
            var err = p.StandardError.ReadToEnd();
            log.Warn($"Unblock-File returned exit={p.ExitCode}. Stderr: {err}");
        }
        else
        {
            log.Info("Successfully unblocked all .ps1 scripts in Scripts directory.");
        }
    }
    catch (Exception ex)
    {
        log.Warn($"Failed to automatically unblock scripts: {ex.Message}");
    }
}

ClearAndBanner();

var workspaceRoot = AppContext.BaseDirectory;
var ctx = new RunContext { WorkspaceRoot = workspaceRoot };

Directory.CreateDirectory(ctx.GetAbsPath("Logs"));
Directory.CreateDirectory(ctx.GetAbsPath("Backup"));
Directory.CreateDirectory(ctx.GetAbsPath("Profiles"));
Directory.CreateDirectory(ctx.GetAbsPath("Scripts"));

ctx.LogFilePath = ctx.GetAbsPath("Logs", $"novaisfps-{ctx.RunId}.log");
ctx.ContextJsonPath = ctx.GetAbsPath("Logs", $"context-{ctx.RunId}.json");

var log = new Logger(ctx.LogFilePath);
log.Info("NOVAIS FPS starting...");

if (!AdminCheck.IsAdministrator())
{
    log.Error("Administrator privileges required. Re-open terminal as Administrator.");
    Environment.Exit(5);
}

using var timer = new TimerResolution();
timer.TryEnable1ms(log);

var detector = new HardwareDetector();
var hw = detector.Collect(log);
ctx.Data["hardware"] = hw;
ctx.SaveJson(ctx.ContextJsonPath);

Console.WriteLine("Menu:");
Console.WriteLine("  1) Run optimizer (Apply)");
Console.WriteLine("  2) Rollback last run (select context json)");
Console.WriteLine("  3) Exit");
Console.Write("Select: ");
var choice = Console.ReadLine()?.Trim();

bool eliteRisk = false;
if (choice == "1")
{
    Console.WriteLine();
    Console.WriteLine("Elite Risk profile: disables certain CPU security mitigations (Spectre/Meltdown)");
    Console.WriteLine("for maximum performance. This INCREASES security risk.");
    eliteRisk = Confirm("Enable Elite Risk profile (NOT recommended for general use)?");
}

var scriptsDir = ctx.GetAbsPath("Scripts");
TryUnblockScripts(scriptsDir, log);

var ps = new PowerShellExecutor(log);

string Script(string name) => Path.Combine(scriptsDir, name);

if (choice == "3" || string.IsNullOrWhiteSpace(choice))
{
    log.Info("Exit.");
    return;
}

if (choice == "2")
{
    Console.WriteLine();
    Console.Write("Path to context json to rollback (default: Logs\\context-<runid>.json): ");
    var path = Console.ReadLine()?.Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(path))
    {
        log.Error("No context json provided.");
        return;
    }

    var abs = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(workspaceRoot, path));
    if (!File.Exists(abs))
    {
        log.Error($"Context json not found: {abs}");
        return;
    }

    // Rollback uses a new run log but reads the provided context json.
    ctx.Data["rollbackTarget"] = abs;
    ctx.SaveJson(ctx.ContextJsonPath);

    log.Warn("Rollback mode: will attempt to revert changes recorded in the target context.");

    var extra = new Dictionary<string, string> { ["-TargetContextJson"] = abs };
    var res = ps.RunScript(Script("01_Backup.ps1"), "Rollback", ctx, extraArgs: extra);
    if (!res.Success) return;

    foreach (var s in new[]
    {
        "03_SystemPower.ps1",
        "04_TimersLatency.ps1",
        "05_CPUScheduler.ps1",
        "07_InputUSB.ps1",
        "08_Network.ps1",
        "09_RegistryTweaks.ps1"
    })
    {
        res = ps.RunScript(Script(s), "Rollback", ctx, extraArgs: extra);
        if (!res.Success) return;
    }

    ps.RunScript(Script("10_Validation.ps1"), "Rollback", ctx, extraArgs: extra);
    log.Info("Rollback finished.");
    return;
}

// APPLY MODE (10 phases)
log.Info("Phase 1/10: Diagnosis (no changes).");
ps.RunScript(Script("02_Diagnosis.ps1"), "Apply", ctx);

log.Info("Phase 2/10: Backup and safety.");
Console.WriteLine();
Console.WriteLine("WARNING: This tool will change Windows settings (power, services, registry).");
Console.WriteLine("All changes are logged and designed to be reversible.");
Console.WriteLine();
if (!Confirm("Continue and create backup/restore point?"))
{
    log.Warn("User cancelled.");
    return;
}
var r = ps.RunScript(Script("01_Backup.ps1"), "Apply", ctx);
if (!r.Success) return;

log.Info("Phase 3/10: System and power.");
r = ps.RunScript(Script("03_SystemPower.ps1"), "Apply", ctx);
if (!r.Success) return;

log.Info("Phase 4/10: Timers and latency (safe defaults; bcdedit optional).");
var enableBcd = Confirm("Apply BCDEdit timer settings (HPET off / platform tick off)? Reversible, may require reboot");
var phase4Args = new Dictionary<string, string> { ["-EnableBcdTweaks"] = enableBcd ? "true" : "false" };
r = ps.RunScript(Script("04_TimersLatency.ps1"), "Apply", ctx, extraArgs: phase4Args);
if (!r.Success) return;

log.Info("Phase 5/10: CPU & scheduler (no global affinity).");
r = ps.RunScript(Script("05_CPUScheduler.ps1"), "Apply", ctx);
if (!r.Success) return;

log.Info("Phase 6/10: GPU & drivers (safe guidance + non-invasive toggles only).");
ps.RunScript(Script("06_GPUDrivers.ps1"), "Apply", ctx);

log.Info("Phase 7/10: Input (USB/HID power saving off).");
r = ps.RunScript(Script("07_InputUSB.ps1"), "Apply", ctx);
if (!r.Success) return;

log.Info("Phase 8/10: Network (safe).");
r = ps.RunScript(Script("08_Network.ps1"), "Apply", ctx);
if (!r.Success) return;

log.Info("Phase 9/10: Registry advanced (reversible).");
var reg9Args = new Dictionary<string, string> { ["-EliteRisk"] = eliteRisk ? "true" : "false" };
r = ps.RunScript(Script("09_RegistryTweaks.ps1"), "Apply", ctx, extraArgs: reg9Args);
if (!r.Success) return;

log.Info("Phase 10/10: Validation and summary.");
ps.RunScript(Script("10_Validation.ps1"), "Apply", ctx);

log.Info($"Done. Log: {ctx.LogFilePath}");
log.Info($"Context: {ctx.ContextJsonPath}");


