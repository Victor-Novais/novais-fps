param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = "",
    [ValidateSet("true","false")][string]$EnableGPUPowerLimit = "false"
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

# GPU Power Limit Management (Elite Competitive Feature)
if ($EnableGPUPowerLimit -eq "true") {
    Write-Log "Applying GPU Power Limit optimization (Elite Competitive)..."
    Write-Log "Setting GPU power limit to maximum (100%) for competitive gaming."
    try {
        $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
        if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
        if (Test-Path $exe) {
            Write-Log "Invoking GPUPowerManager..."
            $proc = Start-Process -FilePath $exe -ArgumentList "--gpu-power-limit" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\novais_gpupower.txt" -RedirectStandardError "$env:TEMP\novais_gpupower.err"
            if (Test-Path "$env:TEMP\novais_gpupower.txt") {
                Get-Content "$env:TEMP\novais_gpupower.txt" | ForEach-Object { Write-Log $_ }
            }
            if ($proc.ExitCode -eq 0) {
                Write-Log "GPU Power Limit optimization applied successfully"
            } else {
                Write-Log -Level "WARN" -Message "GPU Power Limit optimization returned exit code $($proc.ExitCode)"
                Write-Log "Consider using MSI Afterburner or vendor control panel for manual power limit configuration."
            }
        } else {
            Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --gpu-power-limit"
        }
    } catch {
        Write-Log -Level "WARN" -Message "GPU Power Limit optimization failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "GPU Power Limit optimization skipped (user opted out)"
}

exit 0







