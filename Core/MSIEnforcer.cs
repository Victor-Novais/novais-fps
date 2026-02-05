using System.Runtime.Versioning;
using Microsoft.Win32;
using System.Management;

namespace NovaisFPS.Core;

/// <summary>
/// Best-effort MSI/MSI-X enforcer for latency-critical PCI devices.
/// Targets: GPU, Network Adapters, USB Host Controllers.
/// Changes are reversible via manual registry edit or system restore.
/// </summary>
[SupportedOSPlatform("windows")]
public static class MSIEnforcer
{
    // Class GUIDs for common latency-critical devices
    private static readonly HashSet<string> TargetClassGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        "{4d36e968-e325-11ce-bfc1-08002be10318}", // Display adapters (GPU)
        "{4d36e972-e325-11ce-bfc1-08002be10318}", // Network adapters
        "{36fc9e60-c465-11cf-8056-444553540000}", // USB controllers
    };

    public static int Run(Logger log)
    {
        try
        {
            var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, PNPDeviceID, Name, ClassGuid FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'PCI\\\\%'");

            int touched = 0;
            foreach (ManagementObject mo in searcher.Get())
            {
                var classGuid = (mo["ClassGuid"]?.ToString() ?? string.Empty).Trim();
                if (!TargetClassGuids.Contains(classGuid))
                    continue;

                var pnpId = (mo["PNPDeviceID"]?.ToString() ?? string.Empty).Trim();
                var name = (mo["Name"]?.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(pnpId))
                    continue;

                try
                {
                    if (EnableMsiForDevice(pnpId, name, log))
                        touched++;
                }
                catch (Exception ex)
                {
                    log.Warn($"MSIEnforcer: failed for {name} ({pnpId}): {ex.Message}");
                }
            }

            log.Info($"MSIEnforcer: processed devices, MSISupported enabled where applicable. Count={touched}");
            return 0;
        }
        catch (Exception ex)
        {
            log.Error($"MSIEnforcer fatal error: {ex}");
            return 1;
        }
    }

    private static bool EnableMsiForDevice(string pnpDeviceId, string friendlyName, Logger log)
    {
        // Registry path: HKLM\SYSTEM\CurrentControlSet\Enum\<PNPDeviceID>\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties
        var enumPath = $@"SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}";

        using var baseKey = Registry.LocalMachine.OpenSubKey(enumPath, writable: true);
        if (baseKey is null)
        {
            log.Warn($"MSIEnforcer: enum key not found for {friendlyName}: {enumPath}");
            return false;
        }

        using var paramsKey = baseKey.OpenSubKey(@"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties", writable: true)
                           ?? baseKey.CreateSubKey(@"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties", true);

        if (paramsKey is null)
        {
            log.Warn($"MSIEnforcer: cannot open/create MSI properties for {friendlyName}");
            return false;
        }

        var before = paramsKey.GetValue("MSISupported");
        paramsKey.SetValue("MSISupported", 1, RegistryValueKind.DWord);
        var after = paramsKey.GetValue("MSISupported");

        log.Info($"MSIEnforcer: {friendlyName} -> MSISupported {before ?? "<null>"} -> {after}");
        return true;
    }
}






