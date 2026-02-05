using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace NovaisFPS.Core;

/// <summary>
/// Interrupt Steering and NUMA optimization for competitive gaming.
/// Allows granular control of interrupt routing to optimize latency for critical devices
/// (GPU, NIC, USB controllers) by directing their interrupts to specific CPU cores/NUMA nodes.
/// 
/// WARNING: This is an advanced optimization that requires administrator privileges.
/// Incorrect configuration may cause system instability or reduced performance.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InterruptNUMAOptimizer
{
    private readonly Logger _log;

    public InterruptNUMAOptimizer(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Detects NUMA topology and returns information about available NUMA nodes.
    /// </summary>
    public NUMAInfo DetectNUMA()
    {
        var info = new NUMAInfo();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                info.NodeCount = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                info.IsNUMA = Convert.ToBoolean(obj["NUMAEnabled"] ?? false);
            }

            // Get actual NUMA node count
            var nodeCount = GetNumaHighestNodeNumber();
            if (nodeCount >= 0)
            {
                info.NodeCount = nodeCount + 1; // 0-indexed
            }

            _log.Info($"NUMA Detection: Enabled={info.IsNUMA}, Nodes={info.NodeCount}");
        }
        catch (Exception ex)
        {
            _log.Warn($"NUMA detection failed: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// Detects critical PCI/PCIe devices (GPU, NIC, USB controllers).
    /// </summary>
    public List<PCIDevice> DetectCriticalDevices()
    {
        var devices = new List<PCIDevice>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPClass='Display' OR PNPClass='Net' OR PNPClass='USB'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var deviceId = obj["PNPDeviceID"]?.ToString() ?? "";
                var name = obj["Name"]?.ToString() ?? "Unknown";
                var pnpClass = obj["PNPClass"]?.ToString() ?? "";

                if (deviceId.Contains("PCI\\") || deviceId.Contains("PCIe\\"))
                {
                    devices.Add(new PCIDevice
                    {
                        Name = name,
                        DeviceId = deviceId,
                        PNPClass = pnpClass,
                        IsGPU = pnpClass == "Display",
                        IsNIC = pnpClass == "Net",
                        IsUSB = pnpClass == "USB"
                    });
                }
            }

            _log.Info($"Detected {devices.Count} critical PCI/PCIe devices");
            foreach (var dev in devices)
            {
                _log.Debug($"  - {dev.Name} ({dev.PNPClass})");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Device detection failed: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Attempts to configure interrupt affinity for a device using MSI/MSI-X.
    /// This is a best-effort operation as Windows may restrict direct manipulation.
    /// </summary>
    public bool TrySetInterruptAffinity(string deviceId, int[] cpuAffinity, out string error)
    {
        error = "";
        try
        {
            // Note: Direct interrupt affinity manipulation requires kernel-mode drivers or
            // specific Windows APIs that may not be publicly available.
            // This implementation provides a framework that can be extended with:
            // 1. Custom kernel-mode drivers
            // 2. Vendor-specific APIs (NVIDIA, AMD)
            // 3. Registry-based hints (where supported)

            _log.Info($"Attempting to set interrupt affinity for device: {deviceId}");
            _log.Info($"Target CPUs: [{string.Join(", ", cpuAffinity)}]");

            // For now, we log the intent and provide a framework for future implementation
            // Actual implementation would require:
            // - P/Invoke to SetupAPI or DeviceIOControl
            // - Or integration with vendor-specific tools (MSI Afterburner, etc.)

            _log.Warn("Direct interrupt affinity manipulation requires kernel-mode access or vendor APIs.");
            _log.Warn("This feature is currently in framework mode. Consider using vendor tools for GPU interrupt steering.");

            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log.Error($"Failed to set interrupt affinity: {error}");
            return false;
        }
    }

    /// <summary>
    /// Configures NUMA-aware process affinity for a running process.
    /// This is a safer alternative that works at the process level.
    /// </summary>
    public bool SetProcessNUMAffinity(int processId, int numaNode)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var result = SetProcessAffinityMask(process.Handle, (UIntPtr)(1UL << numaNode));
            
            if (result)
            {
                _log.Info($"Set process {processId} affinity to NUMA node {numaNode}");
                return true;
            }
            else
            {
                _log.Warn($"Failed to set NUMA affinity for process {processId}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"NUMA affinity configuration failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Applies interrupt steering optimizations based on detected hardware.
    /// This is an opt-in feature that should be called explicitly.
    /// </summary>
    public OptimizationResult ApplyOptimizations(bool optIn)
    {
        if (!optIn)
        {
            _log.Info("Interrupt Steering optimization skipped (user opted out)");
            return new OptimizationResult { Success = false, Message = "User opted out" };
        }

        var numa = DetectNUMA();
        if (!numa.IsNUMA || numa.NodeCount <= 1)
        {
            _log.Info("NUMA not enabled or single-node system; interrupt steering optimization skipped");
            return new OptimizationResult { Success = false, Message = "NUMA not available" };
        }

        var devices = DetectCriticalDevices();
        if (devices.Count == 0)
        {
            _log.Warn("No critical devices detected for interrupt steering");
            return new OptimizationResult { Success = false, Message = "No devices detected" };
        }

        _log.Info("Interrupt Steering optimization framework initialized");
        _log.Warn("NOTE: Direct interrupt affinity requires kernel-mode drivers or vendor APIs.");
        _log.Warn("Consider using vendor-specific tools (NVIDIA Control Panel, AMD Adrenalin) for GPU interrupt steering.");

        return new OptimizationResult
        {
            Success = true,
            Message = "Framework initialized (requires vendor tools for full implementation)",
            DevicesDetected = devices.Count,
            NUMANodes = numa.NodeCount
        };
    }

    // P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

    [DllImport("kernel32.dll")]
    private static extern int GetNumaHighestNodeNumber(out int highestNodeNumber);

    private int GetNumaHighestNodeNumber()
    {
        if (GetNumaHighestNodeNumber(out var node) == 0)
            return node;
        return -1;
    }
}

public sealed class NUMAInfo
{
    public bool IsNUMA { get; set; }
    public int NodeCount { get; set; }
}

public sealed class PCIDevice
{
    public string Name { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string PNPClass { get; set; } = "";
    public bool IsGPU { get; set; }
    public bool IsNIC { get; set; }
    public bool IsUSB { get; set; }
}

public sealed class OptimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int DevicesDetected { get; set; }
    public int NUMANodes { get; set; }
}
