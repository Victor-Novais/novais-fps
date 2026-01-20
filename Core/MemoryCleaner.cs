using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NovaisFPS.Core;

[SupportedOSPlatform("windows")]
public static class MemoryCleaner
{
    private enum SYSTEM_MEMORY_LIST_COMMAND : int
    {
        MemoryFlushModifiedList = 3,
        MemoryPurgeStandbyList = 4,
        MemoryEmptyWorkingSets = 5
    }

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(
        int SystemInformationClass,
        ref SYSTEM_MEMORY_LIST_COMMAND SystemInformation,
        int SystemInformationLength);

    private const int SystemMemoryListInformation = 80;

    public static int PurgeStandby(Logger log)
    {
        try
        {
            var cmd = SYSTEM_MEMORY_LIST_COMMAND.MemoryPurgeStandbyList;
            var status = NtSetSystemInformation(SystemMemoryListInformation, ref cmd, Marshal.SizeOf<SYSTEM_MEMORY_LIST_COMMAND>());
            if (status != 0)
            {
                log.Warn($"MemoryCleaner: NtSetSystemInformation returned 0x{status:X}");
                return 1;
            }
            log.Info("MemoryCleaner: Standby list purged.");
            return 0;
        }
        catch (Exception ex)
        {
            log.Error($"MemoryCleaner failed: {ex.Message}");
            return 1;
        }
    }
}




