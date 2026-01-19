using System.Security.Principal;
using System.Runtime.Versioning;

namespace NovaisFPS.Core;

[SupportedOSPlatform("windows")]
public static class AdminCheck
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}


