param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = "",
    [ValidateSet("true","false")][string]$EnableBcdTweaks = "false",
    [ValidateSet("true","false")][string]$EnableKernelTickOptimization = "false"
)

. (Join-Path $PSScriptRoot "_Common.ps1")
$script:LogFile = $LogFile

function Get-BcdValue {
    param([Parameter(Mandatory=$true)][string]$Name)
    try {
        $out = (bcdedit /enum "{current}") 2>$null
        $line = ($out | Select-String -Pattern ("^\s*{0}\s" -f [regex]::Escape($Name))) | Select-Object -First 1
        if ($line) { return ($line.Line -split "\s+")[1] }
        return $null
    } catch { return $null }
}

$ctx = Load-JsonFile -Path $ContextJson
if ($null -eq $ctx) { $ctx = [pscustomobject]@{} }

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }

    Write-Log "Rollback: removing bcdedit values we may have set (useplatformclock)."
    try { & bcdedit /deletevalue useplatformclock | Out-Null } catch { }
    Write-Log "Rollback: bcdedit value removed (if present). Reboot may be required."
    exit 0
}

Write-Log "TimersLatency Apply: safe timer recommendations + optional bcdedit changes."

Write-Log "Current bcdedit values (best-effort):"
Write-Log ("  useplatformclock=" + (Get-BcdValue "useplatformclock"))
Write-Log ("  useplatformtick=" + (Get-BcdValue "useplatformtick"))

if ($EnableBcdTweaks -eq "true") {
    Write-Log "Applying bcdedit: useplatformclock=No (prefer TSC)."
    $beforeClock = Get-BcdValue "useplatformclock"
    try {
        & bcdedit /set useplatformclock No | Out-Null
        $afterClock = Get-BcdValue "useplatformclock"
        Add-Change -Ctx $ctx -Category "bcdedit" -Key "useplatformclock" -Before $beforeClock -After $afterClock -Note "Prefer TSC over HPET (reversible)"
        Write-Log "BCDEdit applied. A reboot may be required."
    } catch {
        Write-Log -Level "WARN" -Message "BCDEdit failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "BCDEdit not applied (user opted out)."
    Add-Change -Ctx $ctx -Category "bcdedit" -Key "optOut" -Before $null -After "true" -Note "User opted out of bcdedit"
}

# Optional: call external TimerResolution helper if present (timeBeginPeriod(1)).
try {
    $timerExe = Join-Path $WorkspaceRoot "TimerResolution.exe"
    if (Test-Path $timerExe) {
        Write-Log "Invoking TimerResolution.exe for timeBeginPeriod(1)..."
        Start-Process -FilePath $timerExe -ArgumentList "" -WindowStyle Hidden
    } else {
        Write-Log -Level "WARN" -Message "TimerResolution.exe not found in workspace root; skipping timer helper."
    }
} catch {
    Write-Log -Level "WARN" -Message "Failed to invoke TimerResolution.exe: $($_.Exception.Message)"
}

# Kernel Tick Rate Optimization (Elite Competitive Feature)
if ($EnableKernelTickOptimization -eq "true") {
    Write-Log "Applying Kernel Tick Rate optimization (Elite Competitive)..."
    Write-Log "WARNING: This will modify kernel tick rate to 0.5ms, which may increase CPU usage."
    Write-Log "A reboot will be REQUIRED for changes to take effect."
    try {
        $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
        if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
        if (Test-Path $exe) {
            Write-Log "Invoking KernelTickOptimizer..."
            $proc = Start-Process -FilePath $exe -ArgumentList "--kernel-tick" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\novais_kerneltick.txt" -RedirectStandardError "$env:TEMP\novais_kerneltick.err"
            if (Test-Path "$env:TEMP\novais_kerneltick.txt") {
                Get-Content "$env:TEMP\novais_kerneltick.txt" | ForEach-Object { Write-Log $_ }
            }
            if ($proc.ExitCode -eq 0) {
                Write-Log "Kernel Tick Rate optimization applied successfully"
                Write-Log "REMINDER: Reboot required for kernel tick rate changes to take effect."
            } else {
                Write-Log -Level "WARN" -Message "Kernel Tick Rate optimization returned exit code $($proc.ExitCode)"
            }
        } else {
            Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --kernel-tick"
        }
    } catch {
        Write-Log -Level "WARN" -Message "Kernel Tick Rate optimization failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "Kernel Tick Rate optimization skipped (user opted out)"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0



