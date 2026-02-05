param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = "",
    [ValidateSet("true","false")][string]$EnableInterruptSteering = "false",
    [ValidateSet("true","false")][string]$DisableHyperThreading = "false",
    [ValidateSet("true","false")][string]$DiagnoseCStates = "false"
)

. (Join-Path $PSScriptRoot "_Common.ps1")
$script:LogFile = $LogFile

$ctx = Load-JsonFile -Path $ContextJson
if ($null -eq $ctx) { $ctx = [pscustomobject]@{} }

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }

    Write-Log "Rollback CPU scheduler related registry keys (Game Mode / Game Bar toggles)."
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKCU:\Software\Microsoft\GameBar"
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKCU:\System\GameConfigStore"
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR"
    exit 0
}

Write-Log "CPUScheduler Apply: enabling safe Windows gaming scheduler toggles (no global affinity)."

# Game Mode ON (safe, supported)
Set-RegistryDword -Ctx $ctx -Path "HKCU:\Software\Microsoft\GameBar" -Name "AllowAutoGameMode" -Value 1 -Note "Enable Game Mode"
Set-RegistryDword -Ctx $ctx -Path "HKCU:\Software\Microsoft\GameBar" -Name "AutoGameModeEnabled" -Value 1 -Note "Enable Game Mode"

# Disable background recording (reduces overhead, safe)
Set-RegistryDword -Ctx $ctx -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_Enabled" -Value 0 -Note "Disable Game DVR"
Set-RegistryDword -Ctx $ctx -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" -Name "AppCaptureEnabled" -Value 0 -Note "Disable Game DVR (AppCapture)"

# Optional: affinity & priority helpers
function Set-ProcessAffinityToLastCores {
    param(
        [Parameter(Mandatory=$true)][string[]]$ProcessNames,
        [int]$CoreCountFromEnd = 2
    )
    $total = [Environment]::ProcessorCount
    if ($total -le 1 -or $CoreCountFromEnd -le 0) { return }
    $start = [Math]::Max(0, $total - $CoreCountFromEnd)
    $mask = 0
    for ($i = $start; $i -lt $total; $i++) {
        $mask = $mask -bor (1 -shl $i)
    }

    foreach ($name in $ProcessNames) {
        try {
            $procs = Get-Process -Name $name -ErrorAction SilentlyContinue
            foreach ($p in $procs) {
                $before = $p.ProcessorAffinity
                $p.ProcessorAffinity = [IntPtr]$mask
                Add-Change -Ctx $ctx -Category "affinity" -Key $p.Id -Before $before -After $mask -Note "Pin $name to last $CoreCountFromEnd cores"
                Write-Log "Affinity: $name (PID $($p.Id)) pinned to last $CoreCountFromEnd cores."
            }
        } catch {
            Write-Log -Level "WARN" -Message "Affinity tweak failed for $name: $($_.Exception.Message)"
        }
    }
}

function Boost-ProcessPriority {
    param(
        [Parameter(Mandatory=$true)][string]$ProcessName,
        [ValidateSet("AboveNormal","High","RealTime")][string]$Priority = "High"
    )
    $procs = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
    foreach ($p in $procs) {
        try {
            $before = $p.PriorityClass
            $p.PriorityClass = $Priority
            Add-Change -Ctx $ctx -Category "priority" -Key $p.Id -Before $before -After $Priority -Note "Boost $ProcessName priority"
            Write-Log "Priority: $ProcessName (PID $($p.Id)) $before -> $Priority."
        } catch {
            Write-Log -Level "WARN" -Message "Priority tweak failed for $ProcessName: $($_.Exception.Message)"
        }
    }
}

# Example (commented by default; user can enable manually):
# Set-ProcessAffinityToLastCores -ProcessNames @("SearchIndexer","OneDrive") -CoreCountFromEnd 2
# Boost-ProcessPriority -ProcessName "YourGameExeNameHere" -Priority "High"

# Interrupt Steering and NUMA Optimization (Elite Competitive Feature)
if ($EnableInterruptSteering -eq "true") {
    Write-Log "Applying Interrupt Steering and NUMA optimization (Elite Competitive)..."
    try {
        $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
        if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
        if (Test-Path $exe) {
            Write-Log "Invoking InterruptNUMAOptimizer..."
            $proc = Start-Process -FilePath $exe -ArgumentList "--interrupt-numa" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\novais_interrupt.txt" -RedirectStandardError "$env:TEMP\novais_interrupt.err"
            if (Test-Path "$env:TEMP\novais_interrupt.txt") {
                Get-Content "$env:TEMP\novais_interrupt.txt" | ForEach-Object { Write-Log $_ }
            }
            if ($proc.ExitCode -eq 0) {
                Write-Log "Interrupt Steering optimization applied successfully"
            } else {
                Write-Log -Level "WARN" -Message "Interrupt Steering optimization returned exit code $($proc.ExitCode)"
            }
        } else {
            Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --interrupt-numa"
        }
    } catch {
        Write-Log -Level "WARN" -Message "Interrupt Steering optimization failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "Interrupt Steering optimization skipped (user opted out)"
}

# Hyper-Threading/SMT Management (Elite Competitive - Zero-Level Optimization)
if ($DisableHyperThreading -eq "true") {
    Write-Log "================================================================================"
    Write-Log "ELITE RISK: Hyper-Threading/SMT Disable (Zero-Level Optimization)"
    Write-Log "================================================================================"
    Write-Log "WARNING: Disabling Hyper-Threading (Intel) or SMT (AMD) may reduce latency and"
    Write-Log "improve frametime consistency in some competitive games, BUT:"
    Write-Log "  - SIGNIFICANTLY reduces multi-core performance"
    Write-Log "  - May hurt performance in multi-threaded applications"
    Write-Log "  - Requires reboot to take effect"
    Write-Log "================================================================================"
    
    try {
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        $logicalCores = $cpu.NumberOfLogicalProcessors
        $physicalCores = $cpu.NumberOfCores
        
        if ($logicalCores -eq $physicalCores) {
            Write-Log "Hyper-Threading/SMT is already DISABLED (Logical cores = Physical cores)"
            Add-Change -Ctx $ctx -Category "hyperthreading" -Key "status" -Before "Already Disabled" -After "Already Disabled" -Note "Hyper-Threading/SMT already disabled"
        } else {
            Write-Log "Hyper-Threading/SMT is currently ENABLED"
            Write-Log "  Physical cores: $physicalCores"
            Write-Log "  Logical cores: $logicalCores"
            Write-Log ""
            Write-Log "Attempting to disable Hyper-Threading/SMT via bcdedit..."
            
            # Attempt to disable via bcdedit (may not work on all systems)
            try {
                $before = (bcdedit /enum "{current}" | Select-String "hypervisorlaunchtype") -replace ".*hypervisorlaunchtype\s+", ""
                if ($null -eq $before) { $before = "Not set" }
                
                # Note: bcdedit doesn't directly control Hyper-Threading, but we can try to set processor count
                # This is a workaround - true HT/SMT disable requires BIOS/UEFI
                Write-Log "Attempting to limit processor count to physical cores only..."
                
                # Set number of processors to physical cores (workaround)
                $result = Run-Cmd -File "bcdedit" -Args "/set numproc $physicalCores"
                if ($result -eq 0) {
                    Write-Log "Processor count limited to $physicalCores cores (physical cores only)"
                    Add-Change -Ctx $ctx -Category "hyperthreading" -Key "numproc" -Before "All" -After $physicalCores -Note "Limited processor count to physical cores (workaround for HT/SMT)"
                } else {
                    Write-Log -Level "WARN" -Message "bcdedit numproc failed - may require administrator privileges or BIOS/UEFI access"
                }
                
                Write-Log ""
                Write-Log "NOTE: Full Hyper-Threading/SMT disable requires BIOS/UEFI access:"
                Write-Log "  1. Restart computer and enter BIOS/UEFI (usually F2, F10, DEL during boot)"
                Write-Log "  2. Navigate to: Advanced → CPU Configuration → Hyper-Threading (Intel) or SMT (AMD)"
                Write-Log "  3. Set to: Disabled"
                Write-Log "  4. Save and exit BIOS/UEFI"
                Write-Log "  5. Restart computer"
                Write-Log ""
                Write-Log "WARNING: A reboot is REQUIRED for bcdedit changes to take effect."
                Write-Log "WARNING: To revert, run: bcdedit /deletevalue numproc"
                
                Add-Change -Ctx $ctx -Category "hyperthreading" -Key "recommendation" -Before "Enabled" -After "Disable via BIOS/UEFI" -Note "User opted to disable Hyper-Threading/SMT (bcdedit workaround applied, full disable requires BIOS)"
            } catch {
                Write-Log -Level "WARN" -Message "Hyper-Threading disable attempt failed: $($_.Exception.Message)"
                Write-Log "Falling back to BIOS/UEFI instructions..."
            }
        }
    } catch {
        Write-Log -Level "WARN" -Message "Hyper-Threading detection failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "Hyper-Threading/SMT management skipped (user opted out)"
}

# CPU C-States Management (Zero-Level Optimization - Auto-Execution)
if ($DiagnoseCStates -eq "true") {
    Write-Log "================================================================================"
    Write-Log "CPU C-States Management (Zero-Level Optimization)"
    Write-Log "================================================================================"
    Write-Log "C-States are CPU power-saving states. Disabling deeper C-States (C3, C6, C7) can"
    Write-Log "reduce latency transitions and improve responsiveness, but increases power consumption."
    Write-Log ""
    
    try {
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        Write-Log "CPU: $($cpu.Name)"
        
        # Attempt to disable C-States via powercfg
        Write-Log "Attempting to disable deep C-States via powercfg..."
        
        # Get current power scheme
        $currentScheme = (powercfg /getactivescheme) -replace ".*GUID:\s*", "" -replace "\s.*", ""
        Write-Log "Current power scheme: $currentScheme"
        
        # Disable processor idle states (C-States) via powercfg
        # Note: This may not work on all systems and may require specific power scheme settings
        try {
            # Set processor idle disable to 1 (disable deep C-States)
            $result1 = Run-Cmd -File "powercfg" -Args "/setacvalueindex $currentScheme SUB_PROCESSOR IDLEDISABLE 1"
            $result2 = Run-Cmd -File "powercfg" -Args "/setdcvalueindex $currentScheme SUB_PROCESSOR IDLEDISABLE 1"
            
            if ($result1 -eq 0 -and $result2 -eq 0) {
                # Apply the changes
                Run-Cmd -File "powercfg" -Args "/setactive $currentScheme" | Out-Null
                Write-Log "Deep C-States disabled via powercfg (IDLEDISABLE=1)"
                Add-Change -Ctx $ctx -Category "cstates" -Key "idledisable" -Before "0 (enabled)" -After "1 (disabled)" -Note "Disabled deep C-States via powercfg"
            } else {
                Write-Log -Level "WARN" -Message "powercfg C-States disable failed - may require administrator privileges"
            }
        } catch {
            Write-Log -Level "WARN" -Message "powercfg C-States configuration failed: $($_.Exception.Message)"
        }
        
        # Attempt via registry (alternative method)
        try {
            $processorPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Power"
            $before = Get-RegistryValueSafe -Path $processorPath -Name "ProcessorIdleDisable"
            
            if ($null -eq $before) { $before = 0 }
            
            Set-RegistryDword -Ctx $ctx -Path $processorPath -Name "ProcessorIdleDisable" -Value 1 -Note "Disable processor idle states (C-States) via registry"
            Write-Log "Deep C-States disabled via registry (ProcessorIdleDisable=1)"
        } catch {
            Write-Log -Level "WARN" -Message "Registry C-States configuration failed: $($_.Exception.Message)"
        }
        
        Write-Log ""
        Write-Log "RECOMMENDATION for competitive gaming:"
        Write-Log "  - Disable C3, C6, C7 states (keep C1/C1E enabled for basic power saving)"
        Write-Log "  - This reduces latency spikes from C-State transitions"
        Write-Log "  - Trade-off: Increased power consumption and heat"
        Write-Log ""
        Write-Log "NOTE: Software-based C-States disable may not be fully effective on all systems."
        Write-Log "For maximum effect, also disable C-States in BIOS/UEFI:"
        Write-Log "  1. Restart computer and enter BIOS/UEFI"
        Write-Log "  2. Navigate to: Advanced → CPU Configuration → CPU C-States"
        Write-Log "  3. Set C3, C6, C7 to: Disabled"
        Write-Log "  4. Keep C1/C1E: Enabled (optional)"
        Write-Log "  5. Save and exit BIOS/UEFI"
        Write-Log "  6. Restart computer"
        Write-Log ""
        Write-Log "WARNING: Monitor temperatures after disabling C-States as CPU will consume more power."
        Write-Log "WARNING: To revert, set ProcessorIdleDisable to 0 in registry or re-enable in BIOS/UEFI."
        
    } catch {
        Write-Log -Level "WARN" -Message "C-States management failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "C-States management skipped (user opted out)"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


