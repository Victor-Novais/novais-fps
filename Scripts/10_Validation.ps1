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

# Spectre/Meltdown status
try {
    $mm = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
    $feat = Get-ItemProperty -Path $mm -ErrorAction Stop | Select-Object FeatureSettingsOverride, FeatureSettingsOverrideMask
    Write-Log ("Spectre/Meltdown keys: " + ($feat | ConvertTo-Json -Compress))
} catch {
    Write-Log -Level "WARN" -Message "Spectre/Meltdown keys read failed: $($_.Exception.Message)"
}

# USB selective suspend status
try {
    $usbParam = "HKLM:\SYSTEM\CurrentControlSet\Services\USB\Parameters"
    $usbVals = Get-ItemProperty -Path $usbParam -ErrorAction Stop | Select-Object SelectiveSuspendEnabled
    Write-Log ("USB SelectiveSuspendEnabled: " + ($usbVals | ConvertTo-Json -Compress))
} catch {
    Write-Log -Level "WARN" -Message "USB selective suspend read failed: $($_.Exception.Message)"
}

# Health metrics via helper (--health)
try {
    $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
    if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\\Release\\net8.0-windows\\NovaisFPS.exe" }
    if (Test-Path $exe) {
        Write-Log "Collecting health metrics (--health)..."
        $proc = Start-Process -FilePath $exe -ArgumentList "--health" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\\novais_health.txt" -RedirectStandardError "$env:TEMP\\novais_health.err"
        if (Test-Path "$env:TEMP\\novais_health.txt") {
            Get-Content "$env:TEMP\\novais_health.txt" | ForEach-Object { Write-Log $_ }
        } else {
            Write-Log -Level "WARN" -Message "Health output file not found."
        }
    } else {
        Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --health."
    }
} catch {
    Write-Log -Level "WARN" -Message "Health metrics collection failed: $($_.Exception.Message)"
}

# Simple textual status
Write-Log "Reminder: if BCDEdit was applied, reboot may be required to take full effect."
Write-Log "Validation complete."
exit 0



