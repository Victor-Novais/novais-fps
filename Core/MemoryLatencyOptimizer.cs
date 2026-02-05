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
        var recommendation = new MemoryTimingRecommendation
        {
            ModuleInfo = module,
            CurrentLatency = benchmark?.AverageLatencyMs ?? -1,
            RecommendedTimings = GetRecommendedTimings(module),
            BIOSLocation = GetBIOSLocation(),
            RiskLevel = "Medium-High (incorrect timings can cause instability)",
            Benefits = "Reduced memory latency, improved frametime consistency, lower input lag",
            Warnings = GetWarnings(module)
        };

        return recommendation;
    }

    private string GetRecommendedTimings(MemoryModuleInfo module)
    {
        // Recommendations based on memory speed and type
        // These are general guidelines - actual optimal timings depend on specific memory ICs

        if (module.Speed >= 3600)
        {
            // High-speed DDR4/DDR5: Focus on tightening secondary timings
            return "Primary: tCL-1 to tCL-2, tRCD-1, tRP-1, tRAS-2 to tRAS-4. " +
                   "Secondary: tRFC-10 to tRFC-20, tFAW-2, tWR-2. " +
                   "Gear Mode: Gear 1 (if supported, lower latency than Gear 2).";
        }
        else if (module.Speed >= 3200)
        {
            // Standard DDR4: Moderate tightening
            return "Primary: tCL-1, tRCD-1, tRP-1, tRAS-2. " +
                   "Secondary: tRFC-5 to tRFC-15, tFAW-1, tWR-1. " +
                   "Gear Mode: Gear 1 (if supported).";
        }
        else
        {
            // Lower-speed memory: Conservative tightening
            return "Primary: tCL-1 (if stable), tRCD-0 to tRCD-1, tRP-0 to tRP-1, tRAS-1 to tRAS-2. " +
                   "Secondary: tRFC-5 to tRFC-10, tFAW-1, tWR-1.";
        }
    }

    private string GetBIOSLocation()
    {
        return "BIOS/UEFI → Advanced → Memory Configuration → DRAM Timing Control " +
               "(exact path varies by motherboard vendor: ASUS → Extreme Tweaker, MSI → OC, Gigabyte → Tweaker, etc.)";
    }

    private string GetWarnings(MemoryModuleInfo module)
    {
        return "WARNING: Incorrect memory timings can cause system instability, boot failures, data corruption, " +
               "or permanent hardware damage. Always test stability (e.g., MemTest86) after changes. " +
               "Start with conservative adjustments and test incrementally. Some memory modules may not support " +
               "aggressive timing reductions. Ensure adequate cooling as tighter timings may increase heat.";
    }

    /// <summary>
    /// Collects comprehensive memory latency diagnostics and recommendations.
    /// </summary>
    public MemoryLatencyDiagnosticReport CollectDiagnostics()
    {
        _log.Info("Collecting memory latency diagnostics...");

        var modules = DetectMemoryModules();
        var benchmark = BenchmarkLatency();
        var recommendations = new List<MemoryTimingRecommendation>();

        foreach (var module in modules)
        {
            var rec = GenerateRecommendation(module, benchmark);
            recommendations.Add(rec);
            _log.Info($"Memory timing recommendation for {module.Manufacturer} {module.PartNumber}: {rec.RecommendedTimings}");
        }

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
    public string RecommendedTimings { get; set; } = "";
    public string BIOSLocation { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string Benefits { get; set; } = "";
    public string Warnings { get; set; } = "";
}

public sealed class MemoryLatencyDiagnosticReport
{
    public DateTime Timestamp { get; set; }
    public int ModulesDetected { get; set; }
    public MemoryLatencyResult? BenchmarkResult { get; set; }
    public List<MemoryTimingRecommendation> Recommendations { get; set; } = new();
}
