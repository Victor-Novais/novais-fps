using System.Runtime.InteropServices;

namespace NovaisFPS.Core;

/// <summary>
/// Optional: requests 1ms timer period for the process lifetime while the optimizer runs.
/// This does NOT permanently change Windows; it affects timer coalescing behavior while requested.
/// </summary>
public sealed class TimerResolution : IDisposable
{
    private bool _enabled;

    public bool TryEnable1ms(Logger log)
    {
        // timeBeginPeriod is legacy but still supported. We keep it scoped to this process.
        // Many games already request 1ms; requesting here is safe and reversible on exit.
        var res = timeBeginPeriod(1);
        if (res != 0)
        {
            log.Warn($"timeBeginPeriod(1) failed: {res}");
            return false;
        }

        _enabled = true;
        log.Info("Timer resolution request: 1ms (process-scoped).");
        return true;
    }

    public void Dispose()
    {
        if (_enabled)
            timeEndPeriod(1);
        _enabled = false;
    }

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeEndPeriod(uint uPeriod);
}


