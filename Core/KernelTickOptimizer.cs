using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NovaisFPS.Core;

/// <summary>
/// Kernel Tick Rate optimization for competitive gaming.
/// Attempts to reduce the kernel timer tick rate (e.g., to 0.5ms or 0.25ms) to minimize
/// system latency and improve responsiveness in competitive games.
/// 
/// WARNING: This is an advanced optimization that may increase CPU usage and power consumption.
/// Not all systems support custom kernel tick rates. Requires administrator privileges.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class KernelTickOptimizer
{
    private readonly Logger _log;
    private const string RegistryPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";
    private const string TickRateValueName = "TickRate";

    public KernelTickOptimizer(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Checks if the system supports custom kernel tick rate configuration.
    /// </summary>
    public bool IsSupported()
    {
        try
        {
            // Check Windows version - kernel tick rate adjustment is available on Windows 10 1809+
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major < 10 || (osVersion.Major == 10 && osVersion.Build < 17763))
            {
                _log.Warn($"Kernel tick rate optimization requires Windows 10 1809+ (current: {osVersion})");
                return false;
            }

            // Check if registry key exists and is writable
            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, writable: true);
            return key != null;
        }
        catch (Exception ex)
        {
            _log.Warn($"Kernel tick rate support check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the current kernel tick rate (if configured).
    /// Returns null if not set or unavailable.
    /// </summary>
    public int? GetCurrentTickRate()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, writable: false);
            if (key == null) return null;

            var value = key.GetValue(TickRateValueName);
            if (value is int tickRate)
            {
                return tickRate;
            }
            return null;
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to read kernel tick rate: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sets the kernel tick rate in microseconds.
    /// Common values:
    /// - 500 (0.5ms) - Aggressive, may increase CPU usage
    /// - 250 (0.25ms) - Very aggressive, significant CPU overhead
    /// - 1000 (1ms) - Default on many systems
    /// - 15625 (15.625ms) - Legacy default
    /// 
    /// WARNING: Lower values reduce latency but increase CPU overhead and power consumption.
    /// </summary>
    public bool SetTickRate(int microseconds, out string error)
    {
        error = "";
        
        if (!IsSupported())
        {
            error = "Kernel tick rate optimization not supported on this system";
            return false;
        }

        // Validate range (250-15625 microseconds)
        if (microseconds < 250 || microseconds > 15625)
        {
            error = $"Tick rate must be between 250 and 15625 microseconds (got {microseconds})";
            return false;
        }

        try
        {
            var current = GetCurrentTickRate();
            _log.Info($"Current kernel tick rate: {(current?.ToString() ?? "default")} microseconds");

            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, writable: true);
            if (key == null)
            {
                error = "Failed to open registry key (requires administrator privileges)";
                return false;
            }

            key.SetValue(TickRateValueName, microseconds, RegistryValueKind.DWord);
            _log.Info($"Kernel tick rate set to {microseconds} microseconds ({(microseconds / 1000.0):F2}ms)");
            _log.Warn("A reboot is REQUIRED for kernel tick rate changes to take effect.");
            _log.Warn($"CPU usage may increase by approximately {(15625.0 / microseconds - 1) * 100:F1}%");

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            error = "Administrator privileges required to modify kernel tick rate";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log.Error($"Failed to set kernel tick rate: {error}");
            return false;
        }
    }

    /// <summary>
    /// Restores the default kernel tick rate by removing the registry value.
    /// </summary>
    public bool RestoreDefault(out string error)
    {
        error = "";
        
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, writable: true);
            if (key == null)
            {
                error = "Failed to open registry key";
                return false;
            }

            var current = GetCurrentTickRate();
            if (current == null)
            {
                _log.Info("Kernel tick rate is already at default");
                return true;
            }

            key.DeleteValue(TickRateValueName, throwOnMissingValue: false);
            _log.Info("Kernel tick rate restored to default");
            _log.Warn("A reboot is REQUIRED for changes to take effect.");

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            error = "Administrator privileges required";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log.Error($"Failed to restore default kernel tick rate: {error}");
            return false;
        }
    }

    /// <summary>
    /// Applies recommended kernel tick rate for competitive gaming (500 microseconds = 0.5ms).
    /// This is an opt-in feature.
    /// </summary>
    public KernelTickOptimizationResult ApplyOptimizations(bool optIn)
    {
        if (!optIn)
        {
            _log.Info("Kernel tick rate optimization skipped (user opted out)");
            return new KernelTickOptimizationResult { Success = false, Message = "User opted out" };
        }

        if (!IsSupported())
        {
            return new KernelTickOptimizationResult
            {
                Success = false,
                Message = "Kernel tick rate optimization not supported on this system"
            };
        }

        // Use 500 microseconds (0.5ms) as a balanced aggressive setting
        // Users can manually adjust if needed
        const int recommendedTickRate = 500;

        if (SetTickRate(recommendedTickRate, out var error))
        {
            return new KernelTickOptimizationResult
            {
                Success = true,
                Message = $"Kernel tick rate set to {recommendedTickRate} microseconds (0.5ms). Reboot required.",
                TickRateMicroseconds = recommendedTickRate
            };
        }
        else
        {
            return new KernelTickOptimizationResult
            {
                Success = false,
                Message = error
            };
        }
    }
}

public sealed class KernelTickOptimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int? TickRateMicroseconds { get; set; }
}
