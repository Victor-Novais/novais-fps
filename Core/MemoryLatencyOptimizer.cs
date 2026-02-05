using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Text;

namespace NovaisFPS.Core;

/// <summary>
/// Memory latency optimization diagnostics and recommendations.
/// 
/// Memory latency optimization goes beyond XMP/DOCP, involving manual fine-tuning of
/// primary and secondary timings in BIOS/UEFI. This module diagnoses current memory
/// latency and provides recommendations for fine adjustments.
/// 
/// WARNING: This module only provides diagnostics and recommendations. Direct programmatic
/// changes are NOT implemented due to hardware dependency and potential instability risks.
/// 
/// Users must manually adjust memory timings in BIOS/UEFI.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MemoryLatencyOptimizer
{
    private readonly Logger _log;

    public MemoryLatencyOptimizer(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Collects detailed information about installed RAM modules.
    /// </summary>
    public List<MemoryModuleInfo> DetectMemoryModules()
    {
        var modules = new List<MemoryModuleInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in searcher.Get())
            {
                var module = new MemoryModuleInfo
                {
                    Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown",
                    PartNumber = obj["PartNumber"]?.ToString() ?? "Unknown",
                    Speed = Convert.ToInt32(obj["Speed"] ?? 0),
                    Capacity = Convert.ToUInt64(obj["Capacity"] ?? 0UL),
                    FormFactor = Convert.ToInt32(obj["FormFactor"] ?? 0),
                    MemoryType = Convert.ToInt32(obj["MemoryType"] ?? 0),
                    SerialNumber = obj["SerialNumber"]?.ToString() ?? ""
                };

                // Try to detect XMP/DOCP status
                module.XMPEnabled = DetectXMPStatus(module);

                modules.Add(module);
            }

            _log.Info($"Detected {modules.Count} memory module(s)");
            foreach (var mod in modules)
            {
                _log.Debug($"  - {mod.Manufacturer} {mod.PartNumber} ({mod.Speed} MHz, {mod.Capacity / (1024UL * 1024 * 1024)} GB, XMP: {mod.XMPEnabled})");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Memory module detection failed: {ex.Message}");
        }

        return modules;
    }

    /// <summary>
    /// Attempts to detect if XMP/DOCP is enabled.
    /// This is a best-effort detection as Windows doesn't directly expose this information.
    /// </summary>
    private bool DetectXMPStatus(MemoryModuleInfo module)
    {
        // Windows doesn't directly expose XMP status, but we can infer:
        // - If speed matches JEDEC standard speeds (2133, 2400, 2666, 2933, 3200), likely not XMP
        // - If speed is higher (3600+, 4000+, etc.), likely XMP enabled
        // This is a heuristic and may not be accurate

        var jedecSpeeds = new[] { 2133, 2400, 2666, 2933, 3200 };
        if (jedecSpeeds.Contains(module.Speed))
        {
            return false; // Likely JEDEC (XMP disabled)
        }

        return true; // Likely XMP enabled (higher speeds)
    }

    /// <summary>
    /// Performs a simple memory latency benchmark.
    /// This is a simplified test - for accurate results, use dedicated tools like AIDA64.
    /// </summary>
    public MemoryLatencyResult BenchmarkLatency(int iterations = 1000)
    {
        try
        {
            _log.Info($"Running memory latency benchmark ({iterations} iterations)...");

            var results = new List<double>();
            var arraySize = 1024 * 1024; // 1MB array
            var testArray = new int[arraySize];
            var random = new Random();

            // Warm-up
            for (int i = 0; i < 100; i++)
            {
                _ = testArray[random.Next(arraySize)];
            }

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < iterations; iter++)
            {
                var index = random.Next(arraySize);
                _ = testArray[index];
            }
            sw.Stop();

            var avgLatencyNs = (sw.Elapsed.TotalNanoseconds / iterations);
            var avgLatencyMs = avgLatencyNs / 1_000_000.0;

            _log.Info($"Memory latency benchmark: {avgLatencyMs:F3}ms average ({avgLatencyNs:F0}ns)");

            return new MemoryLatencyResult
            {
                AverageLatencyMs = avgLatencyMs,
                AverageLatencyNs = avgLatencyNs,
                Iterations = iterations,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _log.Warn($"Memory latency benchmark failed: {ex.Message}");
            return new MemoryLatencyResult
            {
                AverageLatencyMs = -1,
                Iterations = 0
            };
        }
    }

    /// <summary>
    /// Generates memory timing recommendations based on detected modules and benchmark results.
    /// </summary>
    public MemoryTimingRecommendation GenerateRecommendation(MemoryModuleInfo module, MemoryLatencyResult? benchmark = null)
    {
        var currentTimings = DetectCurrentTimings(module);
        var recommendation = new MemoryTimingRecommendation
        {
            ModuleInfo = module,
            CurrentLatency = benchmark?.AverageLatencyMs ?? -1,
            CurrentTimings = currentTimings,
            RecommendedTimings = GetRecommendedTimings(module),
            BIOSLocation = GetBIOSLocation(),
            RiskLevel = "Medium-High (incorrect timings can cause instability)",
            Benefits = "Reduced memory latency, improved frametime consistency, lower input lag, better 1% lows",
            Warnings = GetWarnings(module)
        };

        return recommendation;
    }

    /// <summary>
    /// Attempts to read current memory timings from WMI (limited accuracy).
    /// </summary>
    private MemoryTimingsInfo? DetectCurrentTimings(MemoryModuleInfo module)
    {
        // Windows WMI doesn't expose detailed memory timings, but we can try to infer
        // from memory speed and type. For accurate timings, users need to check BIOS/UEFI
        // or use tools like CPU-Z, HWiNFO64, or Thaiphoon Burner.
        
        try
        {
            // This is a placeholder - actual implementation would require:
            // 1. Reading SPD data via SMBus (requires kernel driver)
            // 2. Parsing BIOS/UEFI memory configuration
            // 3. Using third-party tools (CPU-Z, HWiNFO64)
            
            // For now, we provide estimated timings based on JEDEC/XMP standards
            var estimatedTimings = EstimateTimingsFromSpeed(module.Speed, module.MemoryType);
            return estimatedTimings;
        }
        catch
        {
            return null;
        }
    }
    
    private MemoryTimingsInfo EstimateTimingsFromSpeed(int speed, int memoryType)
    {
        // Estimated timings based on common JEDEC/XMP profiles
        // These are approximations - actual timings vary by memory IC and platform
        
        var timings = new MemoryTimingsInfo();
        
        // DDR4 common timings
        if (memoryType == 24 || speed <= 3600) // DDR4 or assumed DDR4
        {
            if (speed >= 3600)
            {
                timings.CL = 18; // Common for 3600MHz
                timings.tRCD = 22;
                timings.tRP = 22;
                timings.tRAS = 42;
                timings.tRFC = 630; // Typical for 16GB modules
                timings.tREFI = 65535; // Default
            }
            else if (speed >= 3200)
            {
                timings.CL = 16; // Common for 3200MHz
                timings.tRCD = 18;
                timings.tRP = 18;
                timings.tRAS = 36;
                timings.tRFC = 560;
                timings.tREFI = 65535;
            }
            else if (speed >= 3000)
            {
                timings.CL = 15;
                timings.tRCD = 17;
                timings.tRP = 17;
                timings.tRAS = 35;
                timings.tRFC = 525;
                timings.tREFI = 65535;
            }
            else
            {
                timings.CL = 15;
                timings.tRCD = 15;
                timings.tRP = 15;
                timings.tRAS = 36;
                timings.tRFC = 350;
                timings.tREFI = 65535;
            }
        }
        else // DDR5
        {
            if (speed >= 6000)
            {
                timings.CL = 30;
                timings.tRCD = 36;
                timings.tRP = 36;
                timings.tRAS = 72;
                timings.tRFC = 560;
                timings.tREFI = 32767;
            }
            else if (speed >= 5600)
            {
                timings.CL = 28;
                timings.tRCD = 34;
                timings.tRP = 34;
                timings.tRAS = 68;
                timings.tRFC = 525;
                timings.tREFI = 32767;
            }
            else
            {
                timings.CL = 28;
                timings.tRCD = 32;
                timings.tRP = 32;
                timings.tRAS = 64;
                timings.tRFC = 490;
                timings.tREFI = 32767;
            }
        }
        
        return timings;
    }
    
    private string GetRecommendedTimings(MemoryModuleInfo module)
    {
        // Recommendations based on memory speed and type
        // These are general guidelines - actual optimal timings depend on specific memory ICs

        var isAMD = IsAMDPlatform();
        var isIntel = IsIntelPlatform();
        var currentTimings = DetectCurrentTimings(module);
        
        var sb = new StringBuilder();
        sb.AppendLine("PRIMARY TIMINGS (Most Impact on Latency):");
        sb.AppendLine("  • tCL (CAS Latency): Reduce by 1-2 cycles if stable");
        sb.AppendLine("  • tRCD (RAS to CAS Delay): Reduce by 1 cycle if stable");
        sb.AppendLine("  • tRP (RAS Precharge): Reduce by 1 cycle if stable");
        sb.AppendLine("  • tRAS (Active to Precharge): Reduce by 2-4 cycles if stable");
        sb.AppendLine("");
        sb.AppendLine("SECONDARY TIMINGS (Fine-Tuning for Competitive Gaming):");
        sb.AppendLine("  • tRFC (Refresh Cycle Time): Reduce by 10-20 cycles (critical for latency)");
        sb.AppendLine("  • tREFI (Refresh Interval): Increase to maximum (65535 for DDR4, 32767 for DDR5)");
        sb.AppendLine("  • tFAW (Four Activate Window): Reduce by 1-2 cycles");
        sb.AppendLine("  • tWR (Write Recovery): Reduce by 1-2 cycles");
        sb.AppendLine("  • tRTP (Read to Precharge): Reduce by 2-4 cycles");
        sb.AppendLine("");
        
        if (module.Speed >= 3600)
        {
            sb.AppendLine("HIGH-SPEED MEMORY (3600MHz+):");
            if (currentTimings != null)
            {
                sb.AppendLine($"  Current Primary: tCL={currentTimings.CL}, tRCD={currentTimings.tRCD}, tRP={currentTimings.tRP}, tRAS={currentTimings.tRAS}");
                sb.AppendLine($"  Recommended Primary: tCL={currentTimings.CL - 1} to {currentTimings.CL - 2}, tRCD={currentTimings.tRCD - 1}, tRP={currentTimings.tRP - 1}, tRAS={currentTimings.tRAS - 2} to {currentTimings.tRAS - 4}");
                sb.AppendLine($"  Current Secondary: tRFC={currentTimings.tRFC}, tREFI={currentTimings.tREFI}");
                sb.AppendLine($"  Recommended Secondary: tRFC={currentTimings.tRFC - 10} to {currentTimings.tRFC - 20}, tREFI=65535 (DDR4) or 32767 (DDR5)");
            }
            
            if (isAMD)
            {
                sb.AppendLine("  AMD-Specific: tRTP-4, tWR-8, Gear Mode: Gear 1 (if supported)");
                sb.AppendLine("  Tertiary: tRRDS-4, tRRDL-6, tFAW-16");
            }
            else if (isIntel)
            {
                sb.AppendLine("  Intel-Specific: Gear Mode: Gear 1 (if supported, lower latency than Gear 2)");
                sb.AppendLine("  Tertiary: tRRDS-4, tRRDL-6, tFAW-16, tWTR_S-4, tWTR_L-8");
            }
        }
        else if (module.Speed >= 3200)
        {
            sb.AppendLine("STANDARD MEMORY (3200-3599MHz):");
            if (currentTimings != null)
            {
                sb.AppendLine($"  Current Primary: tCL={currentTimings.CL}, tRCD={currentTimings.tRCD}, tRP={currentTimings.tRP}, tRAS={currentTimings.tRAS}");
                sb.AppendLine($"  Recommended Primary: tCL={currentTimings.CL - 1}, tRCD={currentTimings.tRCD - 1}, tRP={currentTimings.tRP - 1}, tRAS={currentTimings.tRAS - 2}");
                sb.AppendLine($"  Current Secondary: tRFC={currentTimings.tRFC}, tREFI={currentTimings.tREFI}");
                sb.AppendLine($"  Recommended Secondary: tRFC={currentTimings.tRFC - 5} to {currentTimings.tRFC - 15}, tREFI=65535");
            }
            
            if (isAMD)
            {
                sb.AppendLine("  AMD-Specific: tRTP-4, tWR-8, Gear Mode: Gear 1");
            }
            else if (isIntel)
            {
                sb.AppendLine("  Intel-Specific: Gear Mode: Gear 1");
            }
        }
        else
        {
            sb.AppendLine("LOWER-SPEED MEMORY (<3200MHz):");
            if (currentTimings != null)
            {
                sb.AppendLine($"  Current Primary: tCL={currentTimings.CL}, tRCD={currentTimings.tRCD}, tRP={currentTimings.tRP}, tRAS={currentTimings.tRAS}");
                sb.AppendLine($"  Recommended Primary: tCL={currentTimings.CL - 1} (if stable), tRCD={currentTimings.tRCD - 1}, tRP={currentTimings.tRP - 1}, tRAS={currentTimings.tRAS - 1} to {currentTimings.tRAS - 2}");
                sb.AppendLine($"  Current Secondary: tRFC={currentTimings.tRFC}, tREFI={currentTimings.tREFI}");
                sb.AppendLine($"  Recommended Secondary: tRFC={currentTimings.tRFC - 5} to {currentTimings.tRFC - 10}, tREFI=65535");
            }
            
            if (isAMD)
            {
                sb.AppendLine("  AMD-Specific: tRTP-4, tWR-8");
            }
        }
        
        sb.AppendLine("");
        sb.AppendLine("OPTIMIZATION LINKS:");
        if (isAMD)
        {
            sb.AppendLine("  • AMD Ryzen Memory Tuning Guide: https://www.techpowerup.com/forums/threads/amd-ryzen-memory-tuning-guide.235110/");
            sb.AppendLine("  • Ryzen DRAM Calculator: https://www.techpowerup.com/download/ryzen-dram-calculator/");
        }
        else if (isIntel)
        {
            sb.AppendLine("  • Intel Memory Tuning Guide: https://www.intel.com/content/www/us/en/support/articles/000005629/processors.html");
            sb.AppendLine("  • Memory Overclocking Guide: https://www.overclock.net/threads/memory-overclocking-guide.1751608/");
        }
        
        return sb.ToString();
    }
    
    private bool IsAMDPlatform()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                return name.Contains("AMD", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }
    
    private bool IsIntelPlatform()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                return name.Contains("Intel", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }

    private string GetBIOSLocation()
    {
        return "BIOS/UEFI → Advanced → Memory Configuration → DRAM Timing Control " +
               "(exact path varies by motherboard vendor: ASUS → Extreme Tweaker, MSI → OC, Gigabyte → Tweaker, etc.)";
    }

    private string GetWarnings(MemoryModuleInfo module)
    {
        return "WARNING: Incorrect memory timings can cause system instability, boot failures, data corruption, " +
               "or permanent hardware damage. Always test stability (e.g., MemTest86, HCI MemTest) after changes. " +
               "Start with conservative adjustments and test incrementally. Some memory modules may not support " +
               "aggressive timing reductions. Ensure adequate cooling as tighter timings may increase heat. " +
               "Recommended testing tools: MemTest86 (bootable), HCI MemTest (Windows), TestMem5 (Windows). " +
               "Test for at least 4-8 hours before considering timings stable.";
    }
    
    /// <summary>
    /// Gets comprehensive diagnostics and recommendations for memory latency optimization.
    /// </summary>
    public MemoryLatencyDiagnosticsResult GetDiagnosticsAndRecommendations()
    {
        var modules = DetectMemoryModules();
        var benchmark = BenchmarkLatency();
        var recommendations = new List<MemoryTimingRecommendation>();

        foreach (var module in modules)
        {
            recommendations.Add(GenerateRecommendation(module, benchmark));
        }

        return new MemoryLatencyDiagnosticsResult
        {
            Timestamp = DateTime.UtcNow,
            ModulesDetected = modules.Count,
            BenchmarkResult = benchmark,
            Recommendations = recommendations
        };
    }

    /// <summary>
    /// Collects comprehensive memory latency diagnostics and recommendations.
    /// </summary>
    public MemoryLatencyDiagnosticReport CollectDiagnostics()
    {
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");
        _log.Info("Memory Latency Optimization Diagnostics");
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");
        _log.Info("Collecting memory latency diagnostics...");

        var modules = DetectMemoryModules();
        var benchmark = BenchmarkLatency();
        var recommendations = new List<MemoryTimingRecommendation>();

        foreach (var module in modules)
        {
            var rec = GenerateRecommendation(module, benchmark);
            recommendations.Add(rec);
            
            _log.Info("");
            _log.Info($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _log.Info($"Module: {module.Manufacturer} {module.PartNumber}");
            _log.Info($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _log.Info($"  Speed: {module.Speed} MHz");
            _log.Info($"  Capacity: {module.Capacity / (1024UL * 1024 * 1024)} GB");
            _log.Info($"  XMP/DOCP: {(module.XMPEnabled ? "Enabled" : "Disabled (JEDEC)")}");
            if (rec.CurrentLatency > 0)
            {
                _log.Info($"  Current Latency: {rec.CurrentLatency:F3}ms ({rec.CurrentLatency * 1_000_000:F0}ns)");
            }
            else
            {
                _log.Info($"  Current Latency: Unknown (requires BIOS/UEFI inspection or CPU-Z/HWiNFO64)");
            }
            
            if (rec.CurrentTimings != null)
            {
                _log.Info("");
                _log.Info("  Current Timings (Estimated):");
                _log.Info($"    Primary: tCL={rec.CurrentTimings.CL}, tRCD={rec.CurrentTimings.tRCD}, tRP={rec.CurrentTimings.tRP}, tRAS={rec.CurrentTimings.tRAS}");
                _log.Info($"    Secondary: tRFC={rec.CurrentTimings.tRFC}, tREFI={rec.CurrentTimings.tREFI}");
                _log.Info("");
                _log.Info("  NOTE: These are estimated timings based on memory speed.");
                _log.Info("        For accurate timings, check BIOS/UEFI or use CPU-Z/HWiNFO64.");
            }
            
            _log.Info("");
            _log.Info("  Recommended Timings:");
            var timingLines = rec.RecommendedTimings.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (var line in timingLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _log.Info($"    {line}");
                }
            }
            
            _log.Info("");
            _log.Info($"  Risk Level: {rec.RiskLevel}");
            _log.Info($"  Benefits: {rec.Benefits}");
            _log.Info("");
            _log.Info("  BIOS/UEFI Location:");
            _log.Info($"    {rec.BIOSLocation}");
            _log.Info("");
            _log.Info("  Warnings:");
            var warningLines = rec.Warnings.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (var line in warningLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _log.Info($"    {line}");
                }
            }
        }

        _log.Info("");
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");
        _log.Info($"Summary: {modules.Count} memory module(s) detected");
        if (benchmark != null && benchmark.AverageLatencyMs > 0)
        {
            _log.Info($"Benchmark Latency: {benchmark.AverageLatencyMs:F3}ms ({benchmark.AverageLatencyNs:F0}ns)");
        }
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");

        return new MemoryLatencyDiagnosticReport
        {
            Timestamp = DateTime.UtcNow,
            ModulesDetected = modules.Count,
            BenchmarkResult = benchmark,
            Recommendations = recommendations
        };
    }
}

public sealed class MemoryModuleInfo
{
    public string Manufacturer { get; set; } = "";
    public string PartNumber { get; set; } = "";
    public int Speed { get; set; }
    public ulong Capacity { get; set; }
    public int FormFactor { get; set; }
    public int MemoryType { get; set; }
    public string SerialNumber { get; set; } = "";
    public bool XMPEnabled { get; set; }
}

public sealed class MemoryLatencyResult
{
    public double AverageLatencyMs { get; set; }
    public double AverageLatencyNs { get; set; }
    public int Iterations { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class MemoryTimingRecommendation
{
    public MemoryModuleInfo ModuleInfo { get; set; } = new();
    public double CurrentLatency { get; set; }
    public MemoryTimingsInfo? CurrentTimings { get; set; }
    public string RecommendedTimings { get; set; } = "";
    public string BIOSLocation { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string Benefits { get; set; } = "";
    public string Warnings { get; set; } = "";
}

public sealed class MemoryTimingsInfo
{
    public int CL { get; set; }
    public int tRCD { get; set; }
    public int tRP { get; set; }
    public int tRAS { get; set; }
    public int tRFC { get; set; }
    public int tREFI { get; set; }
}

public sealed class MemoryLatencyDiagnosticReport
{
    public DateTime Timestamp { get; set; }
    public int ModulesDetected { get; set; }
    public MemoryLatencyResult? BenchmarkResult { get; set; }
    public List<MemoryTimingRecommendation> Recommendations { get; set; } = new();
}

public sealed class MemoryLatencyDiagnosticsResult
{
    public DateTime Timestamp { get; set; }
    public int ModulesDetected { get; set; }
    public MemoryLatencyResult? BenchmarkResult { get; set; }
    public List<MemoryTimingRecommendation> Recommendations { get; set; } = new();
}
