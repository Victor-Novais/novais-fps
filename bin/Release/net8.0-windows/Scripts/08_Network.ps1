param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = ""
)

. (Join-Path $PSScriptRoot "_Common.ps1")
$script:LogFile = $LogFile

$ctx = Load-JsonFile -Path $ContextJson
if ($null -eq $ctx) { $ctx = [pscustomobject]@{} }

function Get-NetshTcpGlobals {
    try { return (netsh int tcp show global) } catch { return @() }
}

if ($Mode -eq "Rollback") {
    Write-Log "Rollback Network: returning to safe defaults (autotuning=normal) and reverting per-interface Nagle tweaks."
    try { netsh int tcp set global autotuninglevel=normal | Out-Null } catch { }
    try { netsh int tcp set global ecncapability=default | Out-Null } catch { }
    try { netsh int tcp set global timestamps=default | Out-Null } catch { }

    # Rollback per-interface TcpAckFrequency/TcpNoDelay
    Rollback-RegistryFromChanges -TargetCtx (Load-JsonFile -Path $TargetContextJson) -PrefixMatch "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\"
    exit 0
}

Write-Log "Network Apply: safe TCP globals + per-interface Nagle/ACK tweaks."

$before = (Get-NetshTcpGlobals) -join "`n"
try { netsh int tcp set global autotuninglevel=normal | Out-Null } catch { }
try { netsh int tcp set global ecncapability=disabled | Out-Null } catch { }
try { netsh int tcp set global timestamps=disabled | Out-Null } catch { }
$after = (Get-NetshTcpGlobals) -join "`n"

Add-Change -Ctx $ctx -Category "netsh" -Key "tcpGlobals" -Before $before -After $after -Note "netsh int tcp set global (safe)"

# Per-interface ACK/Nagle tweaks
try {
    $ifaces = Get-NetAdapter -Physical | Where-Object { $_.Status -eq "Up" }
    foreach ($nic in $ifaces) {
        $guid = $nic.InterfaceGuid
        if (-not $guid) { continue }
        $path = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{$guid}"
        Set-RegistryDword -Ctx $ctx -Path $path -Name "TcpAckFrequency" -Value 1 -Note "Immediate ACK for $($nic.Name)"
        Set-RegistryDword -Ctx $ctx -Path $path -Name "TcpNoDelay" -Value 1 -Note "Disable Nagle for $($nic.Name)"
    }
    Write-Log "Network: per-interface TcpAckFrequency/TcpNoDelay applied to $($ifaces.Count) active interfaces."
} catch {
    Write-Log -Level "WARN" -Message "Per-interface network registry tweaks failed: $($_.Exception.Message)"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0



