using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NovaisFPS.Core;

public sealed record HardwareSnapshot
{
    public string OsName { get; init; } = "";
    public string OsVersion { get; init; } = "";
    public string BuildNumber { get; init; } = "";
    public string CpuName { get; init; } = "";
    public int CpuLogicalProcessors { get; init; }
    public ulong TotalRamBytes { get; init; }
    public List<string> Gpus { get; init; } = new();
    public List<string> Disks { get; init; } = new();
    public string CurrentPowerSchemeGuid { get; init; } = "";
}

public sealed class HardwareDetector
{
    [SupportedOSPlatform("windows")]
    public HardwareSnapshot Collect(Logger log)
    {
        var snap = new HardwareSnapshot
        {
            OsName = RuntimeInformation.OSDescription,
            OsVersion = Environment.OSVersion.VersionString,
            CpuLogicalProcessors = Environment.ProcessorCount,
            TotalRamBytes = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
        };

        try
        {
            // OS details
            using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    snap = snap with
                    {
                        OsName = (mo["Caption"]?.ToString() ?? snap.OsName),
                        OsVersion = (mo["Version"]?.ToString() ?? snap.OsVersion),
                        BuildNumber = (mo["BuildNumber"]?.ToString() ?? snap.BuildNumber)
                    };
                    break;
                }
            }

            // CPU
            using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    snap = snap with { CpuName = mo["Name"]?.ToString() ?? snap.CpuName };
                    break;
                }
            }

            // GPUs
            var gpus = new List<string>();
            using (var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    var name = mo["Name"]?.ToString() ?? "Unknown GPU";
                    var drv = mo["DriverVersion"]?.ToString();
                    gpus.Add(drv is { Length: > 0 } ? $"{name} (Driver {drv})" : name);
                }
            }

            // Disks
            var disks = new List<string>();
            using (var searcher = new ManagementObjectSearcher("SELECT Model, InterfaceType, MediaType FROM Win32_DiskDrive"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    var model = mo["Model"]?.ToString() ?? "Disk";
                    var iface = mo["InterfaceType"]?.ToString();
                    var media = mo["MediaType"]?.ToString();
                    var parts = new[] { model, iface, media }.Where(s => !string.IsNullOrWhiteSpace(s));
                    disks.Add(string.Join(" | ", parts));
                }
            }

            snap = snap with { Gpus = gpus, Disks = disks };
        }
        catch (Exception ex)
        {
            log.Warn($"Hardware detection partial failure: {ex.Message}");
        }

        snap = snap with { CurrentPowerSchemeGuid = TryGetActivePowerScheme(log) };
        return snap;
    }

    private static string TryGetActivePowerScheme(Logger log)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/getactivescheme",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi)!;
            var outText = p.StandardOutput.ReadToEnd();
            var errText = p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            if (p.ExitCode != 0)
            {
                log.Warn($"powercfg /getactivescheme failed: {errText}".Trim());
                return "";
            }

            // Output contains GUID; extract first GUID-like token.
            var match = System.Text.RegularExpressions.Regex.Match(outText, @"[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}");
            return match.Success ? match.Value : "";
        }
        catch (Exception ex)
        {
            log.Warn($"powercfg read failed: {ex.Message}");
            return "";
        }
    }
}


