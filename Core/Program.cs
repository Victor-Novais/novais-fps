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

    if (string.Equals(args[0], "--interrupt-numa", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"interruptnuma-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var optimizer = new InterruptNUMAOptimizer(helperLog);
        var result = optimizer.ApplyOptimizations(optIn: true);
        helperLog.Info($"Interrupt Steering: Success={result.Success}, Message={result.Message}, Devices={result.DevicesDetected}, NUMA Nodes={result.NUMANodes}");
        Environment.Exit(result.Success ? 0 : 1);
    }

    if (string.Equals(args[0], "--kernel-tick", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"kerneltick-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var optimizer = new KernelTickOptimizer(helperLog);
        var result = optimizer.ApplyOptimizations(optIn: true);
        helperLog.Info($"Kernel Tick Rate: Success={result.Success}, Message={result.Message}, TickRate={result.TickRateMicroseconds}μs");
        Environment.Exit(result.Success ? 0 : 1);
    }

    if (string.Equals(args[0], "--nvme-optimize", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"nvme-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var optimizer = new NVMeOptimizer(helperLog);
        bool aggressiveWriteCache = args.Length > 1 && string.Equals(args[1], "--nvme-aggressive-write", StringComparison.OrdinalIgnoreCase);
        var result = optimizer.ApplyOptimizations(optIn: true, aggressiveWriteCache: aggressiveWriteCache);
        helperLog.Info($"NVMe Optimization: Success={result.Success}, Message={result.Message}, Devices={result.DevicesDetected}");
        Environment.Exit(result.Success ? 0 : 1);
    }

    if (string.Equals(args[0], "--gpu-power-limit", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"gpupower-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var manager = new GPUPowerManager(helperLog);
        var result = manager.ApplyMaximumPowerLimit(optIn: true);
        helperLog.Info($"GPU Power Limit: Success={result.Success}, Message={result.Message}, Devices={result.DevicesDetected}");
        Environment.Exit(result.Success ? 0 : 1);
    }

    if (string.Equals(args[0], "--latency", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"latency-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var monitor = new EndToEndLatencyMonitor(helperLog);
        var report = monitor.CollectComprehensiveMetrics();
        helperLog.Info($"End-to-End Latency Report:");
        helperLog.Info($"  Input Latency: {report.InputLatency.AverageMs:F3}ms (samples: {report.InputLatency.SampleCount})");
        helperLog.Info($"  Frame-to-Photon: {report.FrameToPhotonLatency.AverageMs:F3}ms ({report.FrameToPhotonLatency.VendorAPI})");
        helperLog.Info($"  DPC: {report.DpcPercent:F2}%, ISR: {report.IsrPercent:F2}%");
        helperLog.Info($"  Timer Resolution: {report.TimerResolutionMs:F3}ms");
        helperLog.Info($"  Overall Latency Score: {report.OverallLatencyScore:F1}/100");
        Environment.Exit(0);
    }

    if (string.Equals(args[0], "--pcie-tlp-diagnose", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"pcietlp-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var optimizer = new PCIeTLPOptimizer(helperLog);
        var report = optimizer.CollectDiagnostics();
        helperLog.Info($"PCIe TLP Diagnostic Report:");
        helperLog.Info($"  Devices Detected: {report.DevicesDetected}");
        helperLog.Info($"  Critical Devices (GPU/NVMe): {report.CriticalDevices}");
        helperLog.Info($"  Recommendations:");
        foreach (var rec in report.Recommendations)
        {
            helperLog.Info($"    Device: {rec.DeviceName}");
            helperLog.Info($"      Current TLP: {rec.CurrentTLPSize}");
            helperLog.Info($"      Recommended: {rec.RecommendedTLPSize}");
            helperLog.Info($"      Reason: {rec.Reason}");
            helperLog.Info($"      BIOS Location: {rec.BIOSLocation}");
            helperLog.Info($"      Risk Level: {rec.RiskLevel}");
            helperLog.Info($"      Benefits: {rec.Benefits}");
            helperLog.Info($"      Warnings: {rec.Warnings}");
        }
        Environment.Exit(0);
    }

    if (string.Equals(args[0], "--memory-latency-diagnose", StringComparison.OrdinalIgnoreCase))
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var logPath = Path.Combine(root, "Logs", $"memlatency-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var helperLog = new Logger(logPath);
        var optimizer = new MemoryLatencyOptimizer(helperLog);
        var report = optimizer.CollectDiagnostics();
        helperLog.Info($"Memory Latency Diagnostic Report:");
        helperLog.Info($"  Modules Detected: {report.ModulesDetected}");
        if (report.BenchmarkResult != null)
        {
            helperLog.Info($"  Benchmark Latency: {report.BenchmarkResult.AverageLatencyMs:F3}ms ({report.BenchmarkResult.AverageLatencyNs:F0}ns)");
        }
        helperLog.Info($"  Recommendations:");
        foreach (var rec in report.Recommendations)
        {
            helperLog.Info($"    Module: {rec.ModuleInfo.Manufacturer} {rec.ModuleInfo.PartNumber}");
            helperLog.Info($"      Speed: {rec.ModuleInfo.Speed} MHz, XMP: {rec.ModuleInfo.XMPEnabled}");
            helperLog.Info($"      Current Latency: {(rec.CurrentLatency > 0 ? $"{rec.CurrentLatency:F3}ms" : "Unknown")}");
            helperLog.Info($"      Recommended Timings: {rec.RecommendedTimings}");
            helperLog.Info($"      BIOS Location: {rec.BIOSLocation}");
            helperLog.Info($"      Risk Level: {rec.RiskLevel}");
            helperLog.Info($"      Benefits: {rec.Benefits}");
            helperLog.Info($"      Warnings: {rec.Warnings}");
        }
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
Console.WriteLine();
Console.WriteLine("Elite Competitive: Kernel Tick Rate optimization reduces kernel timer tick to 0.5ms");
Console.WriteLine("for ultra-low latency. WARNING: Increases CPU usage and REQUIRES reboot.");
var enableKernelTick = Confirm("Enable Kernel Tick Rate optimization (Elite Competitive, opt-in)?");
var phase4Args = new Dictionary<string, string>
{
    ["-EnableBcdTweaks"] = enableBcd ? "true" : "false",
    ["-EnableKernelTickOptimization"] = enableKernelTick ? "true" : "false"
};
r = ps.RunScript(Script("04_TimersLatency.ps1"), "Apply", ctx, extraArgs: phase4Args);
if (!r.Success) return;

log.Info("Phase 5/10: CPU & scheduler (no global affinity).");
Console.WriteLine();
Console.WriteLine("Elite Competitive: Interrupt Steering optimizes interrupt routing for critical devices");
Console.WriteLine("(GPU, NIC, USB) to reduce latency. Requires NUMA-capable system.");
var enableInterruptSteering = Confirm("Enable Interrupt Steering optimization (Elite Competitive, opt-in)?");
var phase5Args = new Dictionary<string, string>
{
    ["-EnableInterruptSteering"] = enableInterruptSteering ? "true" : "false"
};
r = ps.RunScript(Script("05_CPUScheduler.ps1"), "Apply", ctx, extraArgs: phase5Args);
if (!r.Success) return;

log.Info("Phase 6/10: GPU & drivers (safe guidance + non-invasive toggles only).");
Console.WriteLine();
Console.WriteLine("Elite Competitive: GPU Power Limit optimization sets GPU power limit to maximum");
Console.WriteLine("for peak performance during gaming. Requires MSI Afterburner or vendor tools.");
var enableGPUPowerLimit = Confirm("Enable GPU Power Limit optimization (Elite Competitive, opt-in)?");
var phase6Args = new Dictionary<string, string>
{
    ["-EnableGPUPowerLimit"] = enableGPUPowerLimit ? "true" : "false"
};
ps.RunScript(Script("06_GPUDrivers.ps1"), "Apply", ctx, extraArgs: phase6Args);

log.Info("Phase 7/10: Input (USB/HID power saving off).");
r = ps.RunScript(Script("07_InputUSB.ps1"), "Apply", ctx);
if (!r.Success) return;

log.Info("Phase 8/10: Network (safe).");
r = ps.RunScript(Script("08_Network.ps1"), "Apply", ctx);
if (!r.Success) return;

log.Info("Phase 9/10: Registry advanced (reversible).");
Console.WriteLine();
Console.WriteLine("Elite Competitive: NVMe optimization configures NVMe SSDs for maximum performance.");
Console.WriteLine("Aggressive write cache mode improves performance but risks data loss on power failure.");
var enableNVMe = Confirm("Enable NVMe optimization (Elite Competitive, opt-in)?");
bool aggressiveNVMeWriteCache = false;
if (enableNVMe)
{
    Console.WriteLine();
    Console.WriteLine("WARNING: Aggressive write cache disables write cache flushing, which may cause");
    Console.WriteLine("data loss if power is lost. Only enable if you have backups and reliable power.");
    aggressiveNVMeWriteCache = Confirm("Enable aggressive write cache mode (RISKY, opt-in)?");
}
Console.WriteLine();
Console.WriteLine("================================================================================");
Console.WriteLine("EXTREME RISK: Core Isolation / Memory Integrity Disable (Zero-Level)");
Console.WriteLine("================================================================================");
Console.WriteLine("WARNING: Disabling Core Isolation and Memory Integrity removes critical security");
Console.WriteLine("protections. This may provide a small performance benefit but SIGNIFICANTLY");
Console.WriteLine("increases security risk. NOT RECOMMENDED for most users.");
Console.WriteLine("================================================================================");
var disableCoreIsolation = Confirm("Disable Core Isolation/Memory Integrity (EXTREME RISK, opt-in)?");
var reg9Args = new Dictionary<string, string>
{
    ["-EliteRisk"] = eliteRisk ? "true" : "false",
    ["-EnableNVMeOptimization"] = enableNVMe ? "true" : "false",
    ["-AggressiveNVMeWriteCache"] = aggressiveNVMeWriteCache ? "true" : "false",
    ["-DisableCoreIsolation"] = disableCoreIsolation ? "true" : "false"
};
r = ps.RunScript(Script("09_RegistryTweaks.ps1"), "Apply", ctx, extraArgs: reg9Args);
if (!r.Success) return;

log.Info("Phase 10/10: Validation and summary.");
ps.RunScript(Script("10_Validation.ps1"), "Apply", ctx);

log.Info($"Done. Log: {ctx.LogFilePath}");
log.Info($"Context: {ctx.ContextJsonPath}");


