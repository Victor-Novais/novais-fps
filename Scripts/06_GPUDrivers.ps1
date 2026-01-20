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

Write-Log "GPUDrivers $Mode: non-invasive guidance only."
Write-Log "Reason: NVIDIA/AMD low-latency toggles are typically managed via vendor control panels or per-app profiles; forcing via registry is fragile and can break driver state."

try {
    $gpus = Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion
    foreach ($g in $gpus) { Write-Log ("GPU: " + ($g | ConvertTo-Json -Compress)) }
} catch {
    Write-Log -Level "WARN" -Message "GPU detection failed: $($_.Exception.Message)"
}

Write-Log "Recommended (manual) settings:"
Write-Log "  NVIDIA Control Panel:"
Write-Log "    - Low Latency Mode: Ultra"
Write-Log "    - Power management mode: Prefer maximum performance (per-game)"
Write-Log "  AMD Adrenalin:"
Write-Log "    - Anti-Lag: On (per-game)"
Write-Log "    - Chill: Off (unless using as frame limiter intentionally)"

exit 0





