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

Write-Log "Validation $Mode: summarizing key settings (best-effort)."

try { Write-Log ("Power scheme: " + ((powercfg /getactivescheme) 2>$null)) } catch { }
try { Write-Log ("TCP globals: " + ((netsh int tcp show global) -join " | ")) } catch { }

try {
    $sysProfile = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
    $vals = Get-ItemProperty -Path $sysProfile -ErrorAction Stop | Select-Object NetworkThrottlingIndex, SystemResponsiveness
    Write-Log ("SystemProfile: " + ($vals | ConvertTo-Json -Compress))
} catch {
    Write-Log -Level "WARN" -Message "SystemProfile read failed: $($_.Exception.Message)"
}

Write-Log "Reminder: if BCDEdit was applied, reboot may be required to take full effect."
Write-Log "Finished."
exit 0



