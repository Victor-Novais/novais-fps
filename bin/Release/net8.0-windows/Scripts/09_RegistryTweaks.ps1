param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = "",
    [ValidateSet("true","false")][string]$EliteRisk = "false",
    [ValidateSet("true","false")][string]$CleanStandby = "false",
    [ValidateSet("true","false")][string]$EnableNVMeOptimization = "false",
    [ValidateSet("true","false")][string]$AggressiveNVMeWriteCache = "false",
    [ValidateSet("true","false")][string]$DisableCoreIsolation = "false"
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

# Core Isolation / Memory Integrity Disable (Elite Risk - Zero-Level Optimization - Auto-Execution)
if ($DisableCoreIsolation -eq "true") {
    Write-Log "================================================================================"
    Write-Log "EXTREME RISK: Core Isolation / Memory Integrity Disable (Zero-Level Optimization)"
    Write-Log "================================================================================"
    Write-Log "WARNING: Disabling Core Isolation and Memory Integrity removes critical security"
    Write-Log "protections that prevent malware from accessing kernel memory. This optimization"
    Write-Log "may provide a small performance benefit but SIGNIFICANTLY increases security risk."
    Write-Log ""
    Write-Log "RISKS:"
    Write-Log "  - System becomes vulnerable to kernel-level malware attacks"
    Write-Log "  - Reduced protection against advanced persistent threats (APTs)"
    Write-Log "  - May violate security policies in enterprise environments"
    Write-Log "  - NOT RECOMMENDED for systems connected to untrusted networks"
    Write-Log "================================================================================"
    
    try {
        # Multiple registry paths for Core Isolation / Memory Integrity
        $deviceGuardPath = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard"
        $scenariosPath = Join-Path $deviceGuardPath "Scenarios"
        $heciPath = Join-Path $scenariosPath "HypervisorEnforcedCodeIntegrity"
        
        # Ensure paths exist
        New-Item -Path $deviceGuardPath -Force -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path $scenariosPath -Force -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path $heciPath -Force -ErrorAction SilentlyContinue | Out-Null
        
        # Check current status
        $currentEnabled = Get-RegistryValueSafe -Path $heciPath -Name "Enabled"
        $currentWasEnabled = Get-RegistryValueSafe -Path $heciPath -Name "WasEnabledBy"
        
        Write-Log "Current Core Isolation/Memory Integrity status: $currentEnabled"
        if ($currentWasEnabled) {
            Write-Log "Was enabled by: $currentWasEnabled"
        }
        
        if ($currentEnabled -eq 1) {
            Write-Log "Disabling Core Isolation and Memory Integrity via registry..."
            
            # Disable Memory Integrity (Core Isolation)
            Set-RegistryDword -Ctx $ctx -Path $heciPath -Name "Enabled" -Value 0 -Note "Disable Core Isolation/Memory Integrity (EXTREME RISK - reduces security)"
            
            # Also set related values
            try {
                Set-RegistryDword -Ctx $ctx -Path $heciPath -Name "WasEnabledBy" -Value 0 -Note "Clear WasEnabledBy flag"
            } catch {
                Write-Log -Level "WARN" -Message "Failed to set WasEnabledBy: $($_.Exception.Message)"
            }
            
            Write-Log "Core Isolation and Memory Integrity DISABLED via registry."
            Write-Log ""
            Write-Log "═══════════════════════════════════════════════════════════════════════════════"
            Write-Log "CRITICAL SECURITY WARNINGS:"
            Write-Log "═══════════════════════════════════════════════════════════════════════════════"
            Write-Log "  ⚠ A system restart is REQUIRED for changes to take effect."
            Write-Log "  ⚠ Your system is now MORE VULNERABLE to kernel-level malware attacks."
            Write-Log "  ⚠ Advanced persistent threats (APTs) can more easily compromise your system."
            Write-Log "  ⚠ NOT RECOMMENDED for systems connected to untrusted networks."
            Write-Log "  ⚠ NOT RECOMMENDED for systems handling sensitive data."
            Write-Log "  ⚠ Consider re-enabling if you experience security concerns."
            Write-Log ""
            Write-Log "  Expected Performance Benefit:"
            Write-Log "    • Small reduction in memory access latency (typically <1%)"
            Write-Log "    • Minimal impact on gaming performance for most users"
            Write-Log "    • May help in extreme competitive scenarios with high-end hardware"
            Write-Log ""
            Write-Log "  To Re-enable (if needed):"
            Write-Log "    1. Windows Security → Device Security → Core Isolation → Memory Integrity (On)"
            Write-Log "    2. Or set registry: HKLM:\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity\\Enabled = 1"
            Write-Log "    3. Restart computer"
            Write-Log ""
            Write-Log "  Alternative method (if registry doesn't work):"
            Write-Log "    Windows Security → Device Security → Core Isolation → Memory Integrity (Off)"
            Write-Log "    Then restart computer."
            Write-Log "═══════════════════════════════════════════════════════════════════════════════"
        } else {
            Write-Log "Core Isolation and Memory Integrity are already DISABLED."
            Add-Change -Ctx $ctx -Category "coreisolation" -Key "status" -Before "Already Disabled" -After "Already Disabled" -Note "Core Isolation/Memory Integrity already disabled"
        }
        
        # Additional registry tweaks for performance (if Core Isolation is disabled)
        if ($currentEnabled -ne 1) {
            Write-Log "Applying additional performance tweaks for disabled Core Isolation..."
            
            # Disable VBS (Virtualization-Based Security) if Core Isolation is off
            $vbsPath = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard"
            try {
                $currentVbs = Get-RegistryValueSafe -Path $vbsPath -Name "EnableVirtualizationBasedSecurity"
                if ($currentVbs -ne 0) {
                    Set-RegistryDword -Ctx $ctx -Path $vbsPath -Name "EnableVirtualizationBasedSecurity" -Value 0 -Note "Disable VBS (requires Core Isolation off)"
                    Write-Log "VBS (Virtualization-Based Security) disabled."
                }
            } catch {
                Write-Log -Level "WARN" -Message "VBS configuration failed: $($_.Exception.Message)"
            }
        }
        
    } catch {
        Write-Log -Level "WARN" -Message "Core Isolation/Memory Integrity configuration failed: $($_.Exception.Message)"
        Write-Log "NOTE: Core Isolation can also be disabled via:"
        Write-Log "  Windows Security → Device Security → Core Isolation → Memory Integrity (Off)"
        Write-Log "  Then restart computer."
    }
} else {
    Write-Log "Core Isolation/Memory Integrity management skipped (user opted out)"
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

# NVMe Optimization (Elite Competitive Feature)
if ($EnableNVMeOptimization -eq "true") {
    Write-Log "Applying NVMe optimizations (Elite Competitive)..."
    if ($AggressiveNVMeWriteCache -eq "true") {
        Write-Log "WARNING: Aggressive write cache settings may increase risk of data loss on power failure!"
        Write-Log "Ensure you have backups and a reliable power supply before proceeding."
    }
    try {
        $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
        if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
        if (Test-Path $exe) {
            $nvmeArgs = "--nvme-optimize"
            if ($AggressiveNVMeWriteCache -eq "true") {
                $nvmeArgs += " --nvme-aggressive-write"
            }
            Write-Log "Invoking NVMeOptimizer..."
            $proc = Start-Process -FilePath $exe -ArgumentList $nvmeArgs -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\novais_nvme.txt" -RedirectStandardError "$env:TEMP\novais_nvme.err"
            if (Test-Path "$env:TEMP\novais_nvme.txt") {
                Get-Content "$env:TEMP\novais_nvme.txt" | ForEach-Object { Write-Log $_ }
            }
            if ($proc.ExitCode -eq 0) {
                Write-Log "NVMe optimization applied successfully"
            } else {
                Write-Log -Level "WARN" -Message "NVMe optimization returned exit code $($proc.ExitCode)"
            }
        } else {
            Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --nvme-optimize"
        }
    } catch {
        Write-Log -Level "WARN" -Message "NVMe optimization failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "NVMe optimization skipped (user opted out)"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0



