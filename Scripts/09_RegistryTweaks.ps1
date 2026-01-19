param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = "",
    [ValidateSet("true","false")][string]$EliteRisk = "false",
    [ValidateSet("true","false")][string]$CleanStandby = "false"
)

. (Join-Path $PSScriptRoot "_Common.ps1")
$script:LogFile = $LogFile

$ctx = Load-JsonFile -Path $ContextJson
if ($null -eq $ctx) { $ctx = [pscustomobject]@{} }

$sysProfile = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
$gamesTask  = Join-Path $sysProfile "Tasks\Games"
$memMgmt    = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
$memThrottle = Join-Path $memMgmt "Throttle"
$priorityCtrl = "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl"

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }
    Write-Log "Rollback RegistryTweaks (best-effort)."
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch $sysProfile
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch $memMgmt
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch $memThrottle
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch $priorityCtrl
    Rollback-ServicesFromChanges -TargetCtx $target -ServiceNames @("SysMain")
    exit 0
}

Write-Log "RegistryTweaks Apply: safe, documented keys for responsiveness/frametime."

# These are common, widely documented Windows multimedia scheduler knobs.
# We keep values conservative and reversible.

# NetworkThrottlingIndex: disable throttling (ffffffff)
Set-RegistryDword -Ctx $ctx -Path $sysProfile -Name "NetworkThrottlingIndex" -Value 0xffffffff -Note "Disable network throttling for multimedia scheduler"

# SystemResponsiveness: 0 for gaming (default often 20 for multimedia)
Set-RegistryDword -Ctx $ctx -Path $sysProfile -Name "SystemResponsiveness" -Value 0 -Note "Prioritize foreground responsiveness"

# Games task profile
Set-RegistryDword -Ctx $ctx -Path $gamesTask -Name "GPU Priority" -Value 8 -Note "Games task: GPU priority"
Set-RegistryDword -Ctx $ctx -Path $gamesTask -Name "Priority" -Value 6 -Note "Games task: priority"
Set-RegistryString -Ctx $ctx -Path $gamesTask -Name "Scheduling Category" -Value "High" -Note "Games task: scheduling category"
Set-RegistryString -Ctx $ctx -Path $gamesTask -Name "SFIO Priority" -Value "High" -Note "Games task: SFIO priority"

# Kernel / memory management tweaks (conservative)
Set-RegistryDword -Ctx $ctx -Path $memMgmt -Name "LargeSystemCache" -Value 1 -Note "Enable large system cache (server-like file cache behavior)"
Set-RegistryDword -Ctx $ctx -Path $memMgmt -Name "DisablePagingExecutive" -Value 1 -Note "Keep kernel/system code resident in memory (reduce paging)"

# Context Switch / Quantum optimization: short quantum for low latency (Win32PrioritySeparation)
# Value 2 = short quantum, favors foreground (low latency over throughput)
Set-RegistryDword -Ctx $ctx -Path $priorityCtrl -Name "Win32PrioritySeparation" -Value 2 -Note "Short quantum for low-latency context switching (favors foreground)"

# Disable SysMain (Superfetch) to reduce unnecessary I/O latency on SSD
Set-ServiceSafe -Ctx $ctx -Name "SysMain" -StartupType "Disabled" -Action "Stop" -Note "Disable SysMain (Superfetch) for low-latency I/O"

if ($EliteRisk -eq "true") {
    Write-Log "Elite Risk profile ENABLED: applying Spectre/Meltdown mitigation overrides (higher performance, higher risk)."
    # FeatureSettingsOverride / Mask = 3 => disable key mitigations per Microsoft docs.
    Set-RegistryDword -Ctx $ctx -Path $memMgmt -Name "FeatureSettingsOverride" -Value 3 -Note "Disable selected CPU security mitigations (Spectre/Meltdown)"
    Set-RegistryDword -Ctx $ctx -Path $memMgmt -Name "FeatureSettingsOverrideMask" -Value 3 -Note "Mask for FeatureSettingsOverride"

    try {
        Set-RegistryDword -Ctx $ctx -Path $memThrottle -Name "SpectreMitigation" -Value 0 -Note "Throttle: SpectreMitigation=0 (where applicable)"
    } catch {
        Write-Log -Level "WARN" -Message "SpectreMitigation throttle key not applied (OS may not support it): $($_.Exception.Message)"
    }
} else {
    Write-Log "Elite Risk profile DISABLED: CPU security mitigations preserved."
}

if ($CleanStandby -eq "true") {
    try {
        $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
        if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\\Release\\net8.0-windows\\NovaisFPS.exe" }
        if (Test-Path $exe) {
            Write-Log "Invoking MemoryCleaner (standby list purge)..."
            $p = Start-Process -FilePath $exe -ArgumentList "--memory-clean" -Wait -PassThru -WindowStyle Hidden
            Write-Log "MemoryCleaner exit code: $($p.ExitCode)"
        } else {
            Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --memory-clean"
        }
    } catch {
        Write-Log -Level "WARN" -Message "MemoryCleaner invocation failed: $($_.Exception.Message)"
    }
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0



