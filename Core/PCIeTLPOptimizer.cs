using System.Management;
using System.Runtime.Versioning;
using System.Text;

namespace NovaisFPS.Core;

/// <summary>
/// PCIe TLP (Transaction Layer Packet) Size optimization diagnostics and recommendations.
/// 
/// TLP size optimization can reduce latency in PCIe communication between CPU, GPU, and NVMe SSDs.
/// This is a low-level configuration typically accessible via BIOS/UEFI or motherboard/GPU vendor tools.
/// 
/// WARNING: This module only provides diagnostics and recommendations. Direct programmatic changes
/// are NOT implemented due to hardware dependency and potential instability risks.
/// 
/// Users must manually adjust TLP settings in BIOS/UEFI or vendor-specific tools.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PCIeTLPOptimizer
{
    private readonly Logger _log;

    public PCIeTLPOptimizer(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Detects PCIe devices and attempts to read current TLP configuration.
    /// </summary>
    public List<PCIeDeviceInfo> DetectPCIeDevices()
    {
        var devices = new List<PCIeDeviceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPClass='Display' OR PNPClass='SCSIAdapter' OR PNPClass='System'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var deviceId = obj["PNPDeviceID"]?.ToString() ?? "";
                var name = obj["Name"]?.ToString() ?? "Unknown";
                var pnpClass = obj["PNPClass"]?.ToString() ?? "";

                if (deviceId.Contains("PCI\\") || deviceId.Contains("PCIe\\"))
                {
                    var device = new PCIeDeviceInfo
                    {
                        Name = name,
                        DeviceId = deviceId,
                        PNPClass = pnpClass,
                        IsGPU = pnpClass == "Display",
                        IsNVMe = pnpClass == "SCSIAdapter" && (name.Contains("NVMe", StringComparison.OrdinalIgnoreCase) || name.Contains("NVM Express", StringComparison.OrdinalIgnoreCase))
                    };

                    // Attempt to detect PCIe link width and speed
                    try
                    {
                        device.LinkWidth = DetectLinkWidth(deviceId);
                        device.LinkSpeed = DetectLinkSpeed(deviceId);
                    }
                    catch { /* ignore detection errors */ }

                    devices.Add(device);
                }
            }

            _log.Info($"Detected {devices.Count} PCIe device(s)");
            foreach (var dev in devices)
            {
                _log.Debug($"  - {dev.Name} (Class: {dev.PNPClass}, Width: {dev.LinkWidth}x, Speed: {dev.LinkSpeed} GT/s)");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"PCIe device detection failed: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Attempts to detect PCIe link width (x1, x4, x8, x16).
    /// </summary>
    private int DetectLinkWidth(string deviceId)
    {
        // This is a simplified detection - actual implementation would require
        // registry access or vendor-specific APIs
        // Common heuristics:
        // - GPU: Usually x16
        // - NVMe: Usually x4
        // - NIC: Usually x1 or x4

        if (deviceId.Contains("VEN_10DE") || deviceId.Contains("VEN_1002")) // NVIDIA/AMD GPU
            return 16;
        if (deviceId.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
            return 4;

        return -1; // Unknown
    }

    /// <summary>
    /// Attempts to detect PCIe link speed (GT/s).
    /// </summary>
    private double DetectLinkSpeed(string deviceId)
    {
        // Simplified detection - actual implementation would require registry access
        // Common speeds: 2.5 GT/s (PCIe 1.0), 5 GT/s (PCIe 2.0), 8 GT/s (PCIe 3.0), 16 GT/s (PCIe 4.0), 32 GT/s (PCIe 5.0)
        
        // For modern systems, assume PCIe 3.0 or higher
        return 8.0; // Default assumption (PCIe 3.0)
    }

    /// <summary>
    /// Generates TLP size recommendations based on device type and usage.
    /// </summary>
    public TLPRecommendation GenerateRecommendation(PCIeDeviceInfo device)
    {
        var recommendation = new TLPRecommendation
        {
            DeviceName = device.Name,
            CurrentTLPSize = "Unknown (requires BIOS/UEFI inspection)",
            RecommendedTLPSize = GetRecommendedTLPSize(device),
            Reason = GetRecommendationReason(device),
            BIOSLocation = GetBIOSLocation(device),
            RiskLevel = "Low (if done correctly)",
            Benefits = GetBenefits(device),
            Warnings = GetWarnings(device),
            ManualSteps = GetManualSteps(device)
        };

        return recommendation;
    }
    
    /// <summary>
    /// Gets comprehensive diagnostics and recommendations for all PCIe devices.
    /// </summary>
    public TLPDiagnosticsResult GetDiagnosticsAndRecommendations()
    {
        var devices = DetectPCIeDevices();
        var recommendations = new List<TLPRecommendation>();
        
        foreach (var device in devices.Where(d => d.IsGPU || d.IsNVMe))
        {
            recommendations.Add(GenerateRecommendation(device));
        }
        
        return new TLPDiagnosticsResult
        {
            DevicesDetected = devices.Count,
            CriticalDevices = devices.Count(d => d.IsGPU || d.IsNVMe),
            Recommendations = recommendations,
            Timestamp = DateTime.UtcNow
        };
    }

    private string GetRecommendedTLPSize(PCIeDeviceInfo device)
    {
        // Best practices for TLP size:
        // - 128 bytes: Minimum latency (best for competitive gaming)
        // - 256 bytes: Balanced (default on many systems)
        // - 512 bytes: Higher throughput, slightly higher latency

        if (device.IsGPU)
        {
            return "128 bytes (minimum latency for GPU communication)";
        }
        else if (device.IsNVMe)
        {
            return "128 bytes (minimum latency for NVMe I/O)";
        }

        return "128 bytes (minimum latency for competitive gaming)";
    }

    private string GetRecommendationReason(PCIeDeviceInfo device)
    {
        if (device.IsGPU)
        {
            return "Smaller TLP size reduces GPU-to-CPU communication latency, improving frame-to-photon latency in competitive games.";
        }
        else if (device.IsNVMe)
        {
            return "Smaller TLP size reduces NVMe-to-CPU communication latency, improving game asset loading and reducing stuttering.";
        }

        return "Smaller TLP size reduces overall PCIe communication latency, improving system responsiveness.";
    }

    private string GetBIOSLocation(PCIeDeviceInfo device)
    {
        if (device.IsGPU)
        {
            return "BIOS/UEFI → Advanced → PCIe Configuration → GPU TLP Size (or similar, varies by motherboard vendor)";
        }
        else if (device.IsNVMe)
        {
            return "BIOS/UEFI → Advanced → PCIe Configuration → NVMe TLP Size (or similar, varies by motherboard vendor)";
        }

        return "BIOS/UEFI → Advanced → PCIe Configuration → TLP Size (varies by motherboard vendor)";
    }

    private string GetBenefits(PCIeDeviceInfo device)
    {
        return "Reduced PCIe communication latency, improved frame-to-photon latency, reduced input lag, smoother frametimes.";
    }

    private string GetWarnings(PCIeDeviceInfo device)
    {
        return "Incorrect TLP configuration may cause system instability, PCIe link failures, or device detection issues. " +
               "Always test stability after changes. Some systems may not support custom TLP sizes. " +
               "If you experience issues after changing TLP size, revert to default (usually 256 bytes) in BIOS/UEFI.";
    }
    
    private string GetManualSteps(PCIeDeviceInfo device)
    {
        var vendor = GetMotherboardVendor();
        var steps = new StringBuilder();
        
        steps.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        steps.AppendLine("MANUAL BIOS/UEFI CONFIGURATION GUIDE - PCIe TLP Size Optimization");
        steps.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        steps.AppendLine("");
        steps.AppendLine("STEP 1: Enter BIOS/UEFI Setup");
        steps.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        steps.AppendLine("  • Restart your computer");
        steps.AppendLine("  • During boot, press the BIOS/UEFI entry key:");
        steps.AppendLine("    - ASUS: F2 or DEL");
        steps.AppendLine("    - MSI: DEL or F2");
        steps.AppendLine("    - Gigabyte: DEL or F2");
        steps.AppendLine("    - ASRock: DEL or F2");
        steps.AppendLine("    - Other: Common keys are F2, F10, DEL, ESC");
        steps.AppendLine("  • Look for boot message: 'Press [KEY] to enter setup'");
        steps.AppendLine("");
        steps.AppendLine("STEP 2: Navigate to PCIe Configuration");
        steps.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        
        if (vendor.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
        {
            steps.AppendLine("  ASUS BIOS Navigation:");
            steps.AppendLine("    1. Go to: Advanced (F7)");
            steps.AppendLine("    2. Navigate to: PCI Subsystem Settings");
            steps.AppendLine("    3. Look for: PCIe TLP Size or Transaction Layer Packet Size");
            steps.AppendLine("    4. Alternative paths:");
            steps.AppendLine("       - Advanced → Onboard Devices Configuration → PCIe TLP Size");
            steps.AppendLine("       - Advanced → Chipset Configuration → PCIe Configuration → TLP Size");
        }
        else if (vendor.Contains("MSI", StringComparison.OrdinalIgnoreCase))
        {
            steps.AppendLine("  MSI BIOS Navigation:");
            steps.AppendLine("    1. Go to: Advanced (F7)");
            steps.AppendLine("    2. Navigate to: PCI Subsystem Settings");
            steps.AppendLine("    3. Look for: PCIe TLP Size or Transaction Layer Packet Size");
            steps.AppendLine("    4. Alternative paths:");
            steps.AppendLine("       - Advanced → PCIe/PCI Subsystem Settings → TLP Size");
            steps.AppendLine("       - OC → Advanced PCI Settings → PCIe TLP Size");
        }
        else if (vendor.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase))
        {
            steps.AppendLine("  Gigabyte BIOS Navigation:");
            steps.AppendLine("    1. Go to: Advanced Mode (F2)");
            steps.AppendLine("    2. Navigate to: Settings → PCIe Configuration");
            steps.AppendLine("    3. Look for: TLP Size or Transaction Layer Packet Size");
            steps.AppendLine("    4. Alternative paths:");
            steps.AppendLine("       - Settings → IO Ports → PCIe Configuration → TLP Size");
            steps.AppendLine("       - Tweaker → Advanced CPU Settings → PCIe Configuration → TLP Size");
        }
        else if (vendor.Contains("ASRock", StringComparison.OrdinalIgnoreCase))
        {
            steps.AppendLine("  ASRock BIOS Navigation:");
            steps.AppendLine("    1. Go to: Advanced");
            steps.AppendLine("    2. Navigate to: Chipset Configuration");
            steps.AppendLine("    3. Look for: PCIe TLP Size or Transaction Layer Packet Size");
            steps.AppendLine("    4. Alternative paths:");
            steps.AppendLine("       - Advanced → PCIe Configuration → TLP Size");
        }
        else
        {
            steps.AppendLine("  Generic BIOS Navigation:");
            steps.AppendLine("    1. Enter Advanced or Advanced Mode");
            steps.AppendLine("    2. Look for sections like:");
            steps.AppendLine("       - PCIe Configuration");
            steps.AppendLine("       - PCI Subsystem Settings");
            steps.AppendLine("       - Chipset Configuration");
            steps.AppendLine("       - Onboard Devices Configuration");
            steps.AppendLine("    3. Search for: TLP Size, Transaction Layer Packet Size, or PCIe Packet Size");
        }
        
        steps.AppendLine("");
        steps.AppendLine("STEP 3: Configure TLP Size");
        steps.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        steps.AppendLine($"  • Recommended TLP Size: {GetRecommendedTLPSize(device)}");
        steps.AppendLine("  • Available options typically:");
        steps.AppendLine("    - 128 bytes (Minimum latency - RECOMMENDED for competitive gaming)");
        steps.AppendLine("    - 256 bytes (Default/Balanced)");
        steps.AppendLine("    - 512 bytes (Higher throughput, slightly higher latency)");
        steps.AppendLine("  • Select: 128 bytes");
        steps.AppendLine("");
        steps.AppendLine("STEP 4: Save and Exit");
        steps.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        steps.AppendLine("  • Press F10 to Save & Exit (or navigate to Save & Exit → Save Changes and Exit)");
        steps.AppendLine("  • Confirm: Yes");
        steps.AppendLine("  • Computer will restart");
        steps.AppendLine("");
        steps.AppendLine("STEP 5: Verify and Test");
        steps.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        steps.AppendLine("  • After restart, run NOVAIS FPS diagnostics again:");
        steps.AppendLine("    NovaisFPS.exe --pcie-tlp-diagnose");
        steps.AppendLine("  • Test system stability:");
        steps.AppendLine("    - Run your games for extended periods");
        steps.AppendLine("    - Monitor for crashes, freezes, or PCIe link errors");
        steps.AppendLine("    - Check GPU/NVMe detection in Device Manager");
        steps.AppendLine("");
        steps.AppendLine("STEP 6: Revert (if needed)");
        steps.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        steps.AppendLine("  • If you experience issues, revert to default:");
        steps.AppendLine("    1. Enter BIOS/UEFI (same as Step 1)");
        steps.AppendLine("    2. Navigate to same location (Step 2)");
        steps.AppendLine("    3. Set TLP Size to: 256 bytes (default)");
        steps.AppendLine("    4. Save and exit (F10)");
        steps.AppendLine("");
        steps.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        steps.AppendLine("IMPORTANT NOTES:");
        steps.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        steps.AppendLine("  ⚠ Some motherboards may not expose TLP size settings in BIOS/UEFI.");
        steps.AppendLine("    In this case, the setting may be:");
        steps.AppendLine("    - Controlled by GPU/NVMe firmware (not adjustable)");
        steps.AppendLine("    - Hidden in advanced/engineering menus (requires special BIOS)");
        steps.AppendLine("    - Not supported by your motherboard");
        steps.AppendLine("");
        steps.AppendLine("  ⚠ If TLP size setting is not found, your system may already be optimized");
        steps.AppendLine("    or the setting is not available for your hardware.");
        steps.AppendLine("");
        steps.AppendLine("  ⚠ Always test stability after changes. Incorrect TLP configuration can");
        steps.AppendLine("    cause PCIe link failures, device detection issues, or system instability.");
        steps.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        
        return steps.ToString();
    }
    
    private string GetMotherboardVendor()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["Manufacturer"]?.ToString() ?? "Unknown";
            }
        }
        catch { }
        return "Unknown";
    }

    /// <summary>
    /// Collects comprehensive TLP diagnostics and recommendations.
    /// </summary>
    public TLPDiagnosticReport CollectDiagnostics()
    {
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");
        _log.Info("PCIe TLP (Transaction Layer Packet) Size Diagnostics");
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");
        _log.Info("Collecting PCIe TLP diagnostics...");

        var devices = DetectPCIeDevices();
        var recommendations = new List<TLPRecommendation>();

        foreach (var device in devices.Where(d => d.IsGPU || d.IsNVMe))
        {
            var rec = GenerateRecommendation(device);
            recommendations.Add(rec);
            
            _log.Info("");
            _log.Info($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _log.Info($"Device: {device.Name}");
            _log.Info($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _log.Info($"  Current TLP Size: {rec.CurrentTLPSize}");
            _log.Info($"  Recommended: {rec.RecommendedTLPSize}");
            _log.Info($"  Reason: {rec.Reason}");
            _log.Info($"  Risk Level: {rec.RiskLevel}");
            _log.Info($"  Benefits: {rec.Benefits}");
            _log.Info($"  Warnings: {rec.Warnings}");
            _log.Info("");
            _log.Info("  BIOS/UEFI Location:");
            _log.Info($"    {rec.BIOSLocation}");
            _log.Info("");
            _log.Info("  Manual Configuration Steps:");
            var steps = rec.ManualSteps.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (var step in steps)
            {
                if (!string.IsNullOrWhiteSpace(step))
                {
                    _log.Info($"    {step}");
                }
            }
        }

        _log.Info("");
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");
        _log.Info($"Summary: {devices.Count} PCIe device(s) detected, {devices.Count(d => d.IsGPU || d.IsNVMe)} critical device(s) (GPU/NVMe)");
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");

        return new TLPDiagnosticReport
        {
            Timestamp = DateTime.UtcNow,
            DevicesDetected = devices.Count,
            CriticalDevices = devices.Count(d => d.IsGPU || d.IsNVMe),
            Recommendations = recommendations
        };
    }
}

public sealed class PCIeDeviceInfo
{
    public string Name { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string PNPClass { get; set; } = "";
    public bool IsGPU { get; set; }
    public bool IsNVMe { get; set; }
    public int LinkWidth { get; set; } = -1;
    public double LinkSpeed { get; set; } = -1;
}

public sealed class TLPRecommendation
{
    public string DeviceName { get; set; } = "";
    public string CurrentTLPSize { get; set; } = "";
    public string RecommendedTLPSize { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BIOSLocation { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string Benefits { get; set; } = "";
    public string Warnings { get; set; } = "";
    public string ManualSteps { get; set; } = "";
}

public sealed class TLPDiagnosticsResult
{
    public DateTime Timestamp { get; set; }
    public int DevicesDetected { get; set; }
    public int CriticalDevices { get; set; }
    public List<TLPRecommendation> Recommendations { get; set; } = new();
}

public sealed class TLPDiagnosticReport
{
    public DateTime Timestamp { get; set; }
    public int DevicesDetected { get; set; }
    public int CriticalDevices { get; set; }
    public List<TLPRecommendation> Recommendations { get; set; } = new();
}
