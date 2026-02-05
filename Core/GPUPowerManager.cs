using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Text;

namespace NovaisFPS.Core;

/// <summary>
/// Dynamic GPU Power Limit management for competitive gaming.
/// Adjusts GPU power limits to ensure peak performance during gaming sessions
/// and restores power-saving modes when idle.
/// 
/// Supports MSI Afterburner CLI and provides framework for vendor-specific APIs.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GPUPowerManager
{
    private readonly Logger _log;
    private const string MSIAfterburnerPath = @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe";

    public GPUPowerManager(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Detects installed GPU(s) and their vendor.
    /// </summary>
    public List<GPUInfo> DetectGPUs()
    {
        var gpus = new List<GPUInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown";
                var vendor = obj["AdapterCompatibility"]?.ToString() ?? "";
                var driverVersion = obj["DriverVersion"]?.ToString() ?? "";

                var gpu = new GPUInfo
                {
                    Name = name,
                    Vendor = vendor,
                    DriverVersion = driverVersion,
                    IsNVIDIA = name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase),
                    IsAMD = name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase),
                    IsIntel = name.Contains("Intel", StringComparison.OrdinalIgnoreCase)
                };

                gpus.Add(gpu);
            }

            _log.Info($"Detected {gpus.Count} GPU(s)");
            foreach (var gpu in gpus)
            {
                _log.Debug($"  - {gpu.Name} ({gpu.Vendor})");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"GPU detection failed: {ex.Message}");
        }

        return gpus;
    }

    /// <summary>
    /// Checks if MSI Afterburner is installed and available.
    /// </summary>
    public bool IsMSIAfterburnerAvailable()
    {
        if (File.Exists(MSIAfterburnerPath))
        {
            _log.Info("MSI Afterburner detected");
            return true;
        }

        // Check alternative paths
        var altPaths = new[]
        {
            @"C:\Program Files\MSI Afterburner\MSIAfterburner.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MSI Afterburner", "MSIAfterburner.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSI Afterburner", "MSIAfterburner.exe")
        };

        foreach (var path in altPaths)
        {
            if (File.Exists(path))
            {
                _log.Info($"MSI Afterburner detected at: {path}");
                return true;
            }
        }

        _log.Debug("MSI Afterburner not found");
        return false;
    }

    /// <summary>
    /// Sets GPU power limit using MSI Afterburner CLI (if available).
    /// Power limit is specified as a percentage (0-100).
    /// </summary>
    public bool SetPowerLimitMSI(int powerLimitPercent, out string error)
    {
        error = "";
        
        if (!IsMSIAfterburnerAvailable())
        {
            error = "MSI Afterburner not found";
            return false;
        }

        if (powerLimitPercent < 0 || powerLimitPercent > 100)
        {
            error = $"Power limit must be between 0 and 100 (got {powerLimitPercent})";
            return false;
        }

        try
        {
            // MSI Afterburner CLI format: -SetPowerLimit:0,<percentage>
            // The first parameter (0) is the GPU index
            var args = $"-SetPowerLimit:0,{powerLimitPercent}";

            var psi = new ProcessStartInfo
            {
                FileName = MSIAfterburnerPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            if (p == null)
            {
                error = "Failed to start MSI Afterburner process";
                return false;
            }

            p.WaitForExit(5000);
            
            if (p.ExitCode == 0)
            {
                _log.Info($"GPU power limit set to {powerLimitPercent}% via MSI Afterburner");
                return true;
            }
            else
            {
                error = $"MSI Afterburner returned exit code {p.ExitCode}";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log.Error($"Failed to set GPU power limit: {error}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to set GPU power limit using vendor-specific APIs (NVIDIA/AMD).
    /// This is a framework for future implementation with P/Invoke to NVAPI/ADL.
    /// </summary>
    public bool SetPowerLimitVendorAPI(GPUInfo gpu, int powerLimitPercent, out string error)
    {
        error = "";
        
        if (gpu.IsNVIDIA)
        {
            _log.Info("NVIDIA GPU detected - NVAPI integration framework");
            _log.Warn("Direct NVAPI integration requires NVIDIA SDK and P/Invoke declarations");
            _log.Warn("Consider using NVIDIA Control Panel or MSI Afterburner for power limit management");
            error = "NVAPI integration not implemented (use MSI Afterburner or NVIDIA Control Panel)";
            return false;
        }
        else if (gpu.IsAMD)
        {
            _log.Info("AMD GPU detected - ADL integration framework");
            _log.Warn("Direct ADL integration requires AMD ADL SDK and P/Invoke declarations");
            _log.Warn("Consider using AMD Adrenalin or MSI Afterburner for power limit management");
            error = "ADL integration not implemented (use MSI Afterburner or AMD Adrenalin)";
            return false;
        }
        else
        {
            error = $"Vendor API not available for {gpu.Vendor}";
            return false;
        }
    }

    /// <summary>
    /// Applies maximum power limit for competitive gaming (opt-in).
    /// </summary>
    public OptimizationResult ApplyMaximumPowerLimit(bool optIn)
    {
        if (!optIn)
        {
            _log.Info("GPU power limit optimization skipped (user opted out)");
            return new OptimizationResult { Success = false, Message = "User opted out" };
        }

        var gpus = DetectGPUs();
        if (gpus.Count == 0)
        {
            return new OptimizationResult
            {
                Success = false,
                Message = "No GPUs detected"
            };
        }

        _log.Info("Applying maximum GPU power limit for competitive gaming");

        var results = new List<string>();
        bool anySuccess = false;

        foreach (var gpu in gpus)
        {
            // Try MSI Afterburner first
            if (IsMSIAfterburnerAvailable())
            {
                if (SetPowerLimitMSI(100, out var msiError))
                {
                    results.Add($"{gpu.Name}: Power limit set to 100% via MSI Afterburner");
                    anySuccess = true;
                }
                else
                {
                    results.Add($"{gpu.Name}: MSI Afterburner failed - {msiError}");
                }
            }
            else
            {
                // Try vendor API as fallback
                if (SetPowerLimitVendorAPI(gpu, 100, out var vendorError))
                {
                    results.Add($"{gpu.Name}: Power limit set to 100% via vendor API");
                    anySuccess = true;
                }
                else
                {
                    results.Add($"{gpu.Name}: Vendor API failed - {vendorError}");
                    results.Add($"{gpu.Name}: Manual configuration recommended via vendor control panel");
                }
            }
        }

        return new OptimizationResult
        {
            Success = anySuccess,
            Message = string.Join("; ", results),
            DevicesDetected = gpus.Count
        };
    }

    /// <summary>
    /// Restores default/balanced power limit (typically 80-90%).
    /// </summary>
    public bool RestoreDefaultPowerLimit()
    {
        var gpus = DetectGPUs();
        if (gpus.Count == 0) return false;

        bool anySuccess = false;
        foreach (var gpu in gpus)
        {
            // Restore to 85% (typical default)
            if (IsMSIAfterburnerAvailable())
            {
                if (SetPowerLimitMSI(85, out _))
                {
                    _log.Info($"{gpu.Name}: Power limit restored to 85%");
                    anySuccess = true;
                }
            }
        }

        return anySuccess;
    }
}

public sealed class GPUInfo
{
    public string Name { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string DriverVersion { get; set; } = "";
    public bool IsNVIDIA { get; set; }
    public bool IsAMD { get; set; }
    public bool IsIntel { get; set; }
}

public sealed class GPUPowerOptimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int DevicesDetected { get; set; }
}
