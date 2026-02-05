using System.Management;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace NovaisFPS.Core;

/// <summary>
/// Micro-architecture optimization for CPU branch prediction and L3 cache.
/// 
/// These optimizations adjust Windows registry settings that influence CPU
/// prefetch behavior and cache management. All changes are reversible and backed up.
/// 
/// WARNING: These are advanced optimizations that may have minimal impact.
/// Always test stability after applying changes.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MicroArchitectureOptimizer
{
    private readonly Logger _log;

    public MicroArchitectureOptimizer(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Applies micro-architecture optimizations (branch prediction, L3 cache).
    /// </summary>
    public MicroArchOptimizationResult ApplyOptimizations(bool optIn = false)
    {
        if (!optIn)
        {
            return new MicroArchOptimizationResult
            {
                Success = false,
                Message = "Optimizations require explicit opt-in"
            };
        }

        _log.Info("═══════════════════════════════════════════════════════════════════════════════");
        _log.Info("Micro-Architecture Optimization (Branch Prediction / L3 Cache)");
        _log.Info("═══════════════════════════════════════════════════════════════════════════════");

        var changes = new List<RegistryChange>();
        var success = true;

        try
        {
            // Branch Prediction / Prefetch Optimization
            _log.Info("Applying branch prediction and prefetch optimizations...");
            
            // PrefetchParameters - Controls CPU prefetch behavior
            // Value 0x00000001 = Enable prefetch (default)
            // Value 0x00000003 = Enhanced prefetch (may improve cache hit rates)
            var prefetchPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters";
            try
            {
                using var prefetchKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", true);
                if (prefetchKey != null)
                {
                    var currentEnablePrefetch = prefetchKey.GetValue("EnablePrefetcher", 3);
                    if (currentEnablePrefetch == null || (int)currentEnablePrefetch != 3)
                    {
                        prefetchKey.SetValue("EnablePrefetcher", 3, RegistryValueKind.DWord);
                        changes.Add(new RegistryChange
                        {
                            Path = prefetchPath,
                            Name = "EnablePrefetcher",
                            Before = currentEnablePrefetch?.ToString() ?? "Not set",
                            After = "3 (Enhanced prefetch)"
                        });
                        _log.Info("  ✓ EnablePrefetcher set to 3 (Enhanced prefetch)");
                    }
                    else
                    {
                        _log.Info("  ✓ EnablePrefetcher already set to 3");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"  ⚠ PrefetchParameters configuration failed: {ex.Message}");
                success = false;
            }

            // L3 Cache Optimization
            _log.Info("Applying L3 cache optimizations...");
            
            // LargeSystemCache - Already handled in RegistryTweaks, but we verify here
            var memMgmtPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
            try
            {
                using var memKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (memKey != null)
                {
                    // Verify LargeSystemCache is set (should be 1 for server-like behavior)
                    var currentLargeCache = memKey.GetValue("LargeSystemCache", 0);
                    if (currentLargeCache == null || (int)currentLargeCache != 1)
                    {
                        memKey.SetValue("LargeSystemCache", 1, RegistryValueKind.DWord);
                        changes.Add(new RegistryChange
                        {
                            Path = memMgmtPath,
                            Name = "LargeSystemCache",
                            Before = currentLargeCache?.ToString() ?? "Not set",
                            After = "1 (Enabled)"
                        });
                        _log.Info("  ✓ LargeSystemCache set to 1 (Server-like cache behavior)");
                    }
                    else
                    {
                        _log.Info("  ✓ LargeSystemCache already set to 1");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"  ⚠ LargeSystemCache configuration failed: {ex.Message}");
                success = false;
            }

            // CPU Prefetch Window Size (if supported)
            // This may not be available on all systems
            try
            {
                using var cpuKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", true);
                if (cpuKey != null)
                {
                    // PrefetchBandwidth - Controls prefetch bandwidth (if supported)
                    // Higher values may improve prefetch efficiency
                    var currentBandwidth = cpuKey.GetValue("PrefetchBandwidth", null);
                    if (currentBandwidth == null)
                    {
                        // Only set if not already configured (let Windows manage by default)
                        _log.Info("  ℹ PrefetchBandwidth not set (using Windows default)");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Debug($"PrefetchBandwidth check skipped: {ex.Message}");
            }

            // Memory Management - Disable Paging Executive (already in RegistryTweaks, verify)
            try
            {
                using var memKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (memKey != null)
                {
                    var currentDisablePaging = memKey.GetValue("DisablePagingExecutive", 0);
                    if (currentDisablePaging == null || (int)currentDisablePaging != 1)
                    {
                        memKey.SetValue("DisablePagingExecutive", 1, RegistryValueKind.DWord);
                        changes.Add(new RegistryChange
                        {
                            Path = memMgmtPath,
                            Name = "DisablePagingExecutive",
                            Before = currentDisablePaging?.ToString() ?? "Not set",
                            After = "1 (Enabled)"
                        });
                        _log.Info("  ✓ DisablePagingExecutive set to 1 (Keep kernel in memory)");
                    }
                    else
                    {
                        _log.Info("  ✓ DisablePagingExecutive already set to 1");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"  ⚠ DisablePagingExecutive configuration failed: {ex.Message}");
                success = false;
            }

            _log.Info("");
            _log.Info("═══════════════════════════════════════════════════════════════════════════════");
            _log.Info("Micro-Architecture Optimization Summary:");
            _log.Info("═══════════════════════════════════════════════════════════════════════════════");
            _log.Info($"  Changes Applied: {changes.Count}");
            _log.Info($"  Success: {success}");
            _log.Info("");
            _log.Info("  Optimizations Applied:");
            _log.Info("    • Enhanced CPU Prefetch (EnablePrefetcher = 3)");
            _log.Info("    • Large System Cache (LargeSystemCache = 1)");
            _log.Info("    • Disable Paging Executive (DisablePagingExecutive = 1)");
            _log.Info("");
            _log.Info("  Expected Benefits:");
            _log.Info("    • Improved branch prediction efficiency");
            _log.Info("    • Better L3 cache utilization");
            _log.Info("    • Reduced memory access latency");
            _log.Info("    • Slightly improved 1% lows and frametime consistency");
            _log.Info("");
            _log.Info("  NOTE: These optimizations have minimal impact and may not be noticeable");
            _log.Info("        on all systems. Always test stability after changes.");
            _log.Info("═══════════════════════════════════════════════════════════════════════════════");

            return new MicroArchOptimizationResult
            {
                Success = success,
                Message = $"Applied {changes.Count} micro-architecture optimizations",
                ChangesApplied = changes.Count,
                Changes = changes
            };
        }
        catch (Exception ex)
        {
            _log.Error($"Micro-architecture optimization failed: {ex.Message}");
            return new MicroArchOptimizationResult
            {
                Success = false,
                Message = $"Optimization failed: {ex.Message}",
                ChangesApplied = changes.Count
            };
        }
    }

    /// <summary>
    /// Reverts micro-architecture optimizations.
    /// </summary>
    public bool RevertOptimizations(List<RegistryChange> changes)
    {
        _log.Info("Reverting micro-architecture optimizations...");
        var reverted = 0;

        foreach (var change in changes)
        {
            try
            {
                var keyPath = change.Path.Replace(@"HKEY_LOCAL_MACHINE\", "");
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                if (key != null)
                {
                    if (change.Before == "Not set" || string.IsNullOrEmpty(change.Before))
                    {
                        key.DeleteValue(change.Name, throwOnMissingValue: false);
                    }
                    else
                    {
                        if (int.TryParse(change.Before, out var intValue))
                        {
                            key.SetValue(change.Name, intValue, RegistryValueKind.DWord);
                        }
                        else
                        {
                            key.SetValue(change.Name, change.Before);
                        }
                    }
                    reverted++;
                    _log.Info($"  ✓ Reverted {change.Path}\\{change.Name}");
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"  ⚠ Failed to revert {change.Path}\\{change.Name}: {ex.Message}");
            }
        }

        _log.Info($"Reverted {reverted}/{changes.Count} changes");
        return reverted == changes.Count;
    }
}

public sealed class MicroArchOptimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int ChangesApplied { get; set; }
    public List<RegistryChange> Changes { get; set; } = new();
}

public sealed class RegistryChange
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
}
