using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NovaisFPS.Core;

/// <summary>
/// NVMe-specific I/O optimizations for competitive gaming.
/// Configures NVMe SSDs for maximum performance by adjusting cache policies,
/// power management, and I/O scheduling priorities.
/// 
/// WARNING: Some optimizations may reduce data safety guarantees.
/// Always ensure backups are in place before applying write cache changes.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NVMeOptimizer
{
    private readonly Logger _log;

    public NVMeOptimizer(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Detects NVMe drives in the system.
    /// </summary>
    public List<NVMeDrive> DetectNVMeDrives()
    {
        var drives = new List<NVMeDrive>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='NVMe' OR MediaType='NVMe'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var drive = new NVMeDrive
                {
                    DeviceId = obj["DeviceID"]?.ToString() ?? "",
                    Model = obj["Model"]?.ToString() ?? "Unknown",
                    SerialNumber = obj["SerialNumber"]?.ToString() ?? "",
                    Size = Convert.ToUInt64(obj["Size"] ?? 0UL)
                };

                // Get partition info to find drive letters
                try
                {
                    var partitionQuery = $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{drive.DeviceId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                    using var partitionSearcher = new ManagementObjectSearcher(partitionQuery);
                    foreach (ManagementObject partition in partitionSearcher.Get())
                    {
                        var logicalQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition";
                        using var logicalSearcher = new ManagementObjectSearcher(logicalQuery);
                        foreach (ManagementObject logical in logicalSearcher.Get())
                        {
                            drive.DriveLetters.Add(logical["DeviceID"]?.ToString() ?? "");
                        }
                    }
                }
                catch { /* ignore partition enumeration errors */ }

                drives.Add(drive);
            }

            _log.Info($"Detected {drives.Count} NVMe drive(s)");
            foreach (var drive in drives)
            {
                _log.Debug($"  - {drive.Model} ({drive.Size / (1024UL * 1024 * 1024)} GB) - {string.Join(", ", drive.DriveLetters)}");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"NVMe detection failed: {ex.Message}");
        }

        return drives;
    }

    /// <summary>
    /// Configures write cache buffer flushing policy for NVMe drives.
    /// WARNING: Disabling write cache flushing improves performance but risks data loss on power failure.
    /// </summary>
    public bool ConfigureWriteCache(string deviceId, bool enableFlushing, out string error)
    {
        error = "";
        try
        {
            // Write cache policy is typically managed via disk properties or registry
            // Registry path: HKLM\SYSTEM\CurrentControlSet\Enum\PCI\<device>\<instance>\Device Parameters\Disk
            // Value: WriteCacheEnabled (DWORD)

            _log.Info($"Configuring write cache for device: {deviceId}");
            _log.Warn($"Write cache flushing: {(enableFlushing ? "ENABLED (safer)" : "DISABLED (faster, risky)")}");

            // Note: Direct registry manipulation for write cache is complex and device-specific
            // This implementation provides a framework that can be extended
            // For production use, consider using diskpart or vendor-specific tools

            _log.Warn("Write cache configuration requires device-specific registry keys.");
            _log.Warn("Consider using diskpart or vendor utilities for reliable write cache management.");

            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log.Error($"Failed to configure write cache: {error}");
            return false;
        }
    }

    /// <summary>
    /// Sets power management policy for NVMe drives to maximum performance.
    /// </summary>
    public bool SetPowerManagementPolicy(string deviceId, bool maximumPerformance, out string error)
    {
        error = "";
        try
        {
            // Power management for storage devices is typically controlled via:
            // 1. Device Manager -> Disk drives -> Properties -> Power Management
            // 2. Registry: HKLM\SYSTEM\CurrentControlSet\Enum\PCI\<device>\<instance>\Device Parameters\Disk
            //    Value: EnableIdlePowerManagement (DWORD: 0 = disabled, 1 = enabled)

            _log.Info($"Setting power management for device: {deviceId}");
            _log.Info($"Maximum performance mode: {maximumPerformance}");

            // Framework implementation - actual registry manipulation would require device enumeration
            _log.Warn("Power management configuration requires device-specific registry access.");
            _log.Warn("Consider using Device Manager or vendor utilities for reliable power management.");

            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log.Error($"Failed to set power management: {error}");
            return false;
        }
    }

    /// <summary>
    /// Configures TRIM scheduling to avoid interference during gaming sessions.
    /// </summary>
    public bool ConfigureTRIM(bool optimizeForGaming, out string error)
    {
        error = "";
        try
        {
            // TRIM is typically managed via:
            // 1. Registry: HKLM\SYSTEM\CurrentControlSet\Control\Storage
            // 2. Scheduled tasks (Defrag/TRIM tasks)
            // 3. Disk properties

            _log.Info($"Configuring TRIM optimization: Gaming mode = {optimizeForGaming}");

            if (optimizeForGaming)
            {
                // Disable automatic TRIM during active hours (gaming sessions)
                // This prevents I/O spikes that could cause frame drops
                _log.Info("TRIM optimization: Disabling automatic TRIM during active hours");
                _log.Info("Manual TRIM can still be performed via 'Optimize Drives' when not gaming");
            }

            // Framework implementation
            _log.Warn("TRIM configuration requires system-level changes.");
            _log.Warn("Consider using Windows Storage Optimizer settings for TRIM management.");

            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log.Error($"Failed to configure TRIM: {error}");
            return false;
        }
    }

    /// <summary>
    /// Applies NVMe optimizations (opt-in).
    /// </summary>
    public OptimizationResult ApplyOptimizations(bool optIn, bool aggressiveWriteCache)
    {
        if (!optIn)
        {
            _log.Info("NVMe optimization skipped (user opted out)");
            return new OptimizationResult { Success = false, Message = "User opted out" };
        }

        var drives = DetectNVMeDrives();
        if (drives.Count == 0)
        {
            _log.Info("No NVMe drives detected; optimization skipped");
            return new OptimizationResult
            {
                Success = false,
                Message = "No NVMe drives detected"
            };
        }

        _log.Info($"Applying NVMe optimizations to {drives.Count} drive(s)");

        var results = new List<string>();
        bool anySuccess = false;

        foreach (var drive in drives)
        {
            // Power management: Maximum performance
            if (SetPowerManagementPolicy(drive.DeviceId, true, out var pmError))
            {
                results.Add($"{drive.Model}: Power management optimized");
                anySuccess = true;
            }
            else
            {
                results.Add($"{drive.Model}: Power management - {pmError}");
            }

            // Write cache: User choice (with warning)
            if (aggressiveWriteCache)
            {
                _log.Warn($"WARNING: Disabling write cache flushing on {drive.Model} may cause data loss on power failure!");
                if (ConfigureWriteCache(drive.DeviceId, false, out var wcError))
                {
                    results.Add($"{drive.Model}: Write cache optimized (flushing disabled)");
                    anySuccess = true;
                }
                else
                {
                    results.Add($"{drive.Model}: Write cache - {wcError}");
                }
            }

            // TRIM: Optimize for gaming
            if (ConfigureTRIM(true, out var trimError))
            {
                results.Add($"{drive.Model}: TRIM optimized for gaming");
                anySuccess = true;
            }
            else
            {
                results.Add($"{drive.Model}: TRIM - {trimError}");
            }
        }

        return new OptimizationResult
        {
            Success = anySuccess,
            Message = string.Join("; ", results),
            DevicesDetected = drives.Count
        };
    }
}

public sealed class NVMeDrive
{
    public string DeviceId { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public ulong Size { get; set; }
    public List<string> DriveLetters { get; set; } = new();
}

public sealed class NVMeOptimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int DevicesDetected { get; set; }
}
