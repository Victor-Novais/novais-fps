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
    Write-Log "Rollback Network: returning to safe defaults (autotuning=normal)."
    try { netsh int tcp set global autotuninglevel=normal | Out-Null } catch { }
    try { netsh int tcp set global ecncapability=default | Out-Null } catch { }
    try { netsh int tcp set global timestamps=default | Out-Null } catch { }
    exit 0
}

Write-Log "Network Apply: safe TCP stack settings (no MTU, no VPN, no aggressive tweaks)."

$before = (Get-NetshTcpGlobals) -join "`n"
try { netsh int tcp set global autotuninglevel=normal | Out-Null } catch { }
try { netsh int tcp set global ecncapability=disabled | Out-Null } catch { }
try { netsh int tcp set global timestamps=disabled | Out-Null } catch { }
$after = (Get-NetshTcpGlobals) -join "`n"

Add-Change -Ctx $ctx -Category "netsh" -Key "tcpGlobals" -Before $before -After $after -Note "netsh int tcp set global (safe)"
Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


