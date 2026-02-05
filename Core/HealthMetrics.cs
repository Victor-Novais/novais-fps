using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NovaisFPS.Core;

[SupportedOSPlatform("windows")]
public sealed class HealthSnapshot
{
    public double DpcPercent { get; init; }
    public double IsrPercent { get; init; }
    public double CurrentTimerResolutionMs { get; init; }
    public double MinTimerResolutionMs { get; init; }
    public double MaxTimerResolutionMs { get; init; }
}

[SupportedOSPlatform("windows")]
public static class HealthMetrics
{
    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(
        out uint MinimumResolution,
        out uint MaximumResolution,
        out uint CurrentResolution);

    public static HealthSnapshot Collect(Logger log)
    {
        double dpc = TryReadCounter("Processor", "% DPC Time", "_Total", log);
        double isr = TryReadCounter("Processor", "% Interrupt Time", "_Total", log);

        var (minMs, maxMs, curMs) = TryQueryTimerResolution(log);

        return new HealthSnapshot
        {
            DpcPercent = dpc,
            IsrPercent = isr,
            MinTimerResolutionMs = minMs,
            MaxTimerResolutionMs = maxMs,
            CurrentTimerResolutionMs = curMs
        };
    }

    private static double TryReadCounter(string category, string counter, string instance, Logger log)
    {
        try
        {
            using var pc = new PerformanceCounter(category, counter, instance, readOnly: true);
            _ = pc.NextValue();
            System.Threading.Thread.Sleep(200);
            return Math.Round(pc.NextValue(), 2);
        }
        catch (Exception ex)
        {
            log.Warn($"HealthMetrics: counter {category}\\{counter}\\{instance} failed: {ex.Message}");
            return -1;
        }
    }

    private static (double minMs, double maxMs, double curMs) TryQueryTimerResolution(Logger log)
    {
        try
        {
            var rc = NtQueryTimerResolution(out var min, out var max, out var cur);
            if (rc != 0) throw new InvalidOperationException($"NtQueryTimerResolution returned 0x{rc:X}");
            // values are in 100-ns units
            double toMs(uint v) => Math.Round(v / 10000.0, 3);
            return (toMs(min), toMs(max), toMs(cur));
        }
        catch (Exception ex)
        {
            log.Warn($"HealthMetrics: timer resolution query failed: {ex.Message}");
            return (-1, -1, -1);
        }
    }
}






