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
    Write-Log "  - Requires BIOS/UEFI access to change"
    Write-Log "  - May require system restart"
    Write-Log "================================================================================"
    
    try {
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        $logicalCores = $cpu.NumberOfLogicalProcessors
        $physicalCores = $cpu.NumberOfCores
        
        if ($logicalCores -eq $physicalCores) {
            Write-Log "Hyper-Threading/SMT is already DISABLED (Logical cores = Physical cores)"
        } else {
            Write-Log "Hyper-Threading/SMT is currently ENABLED"
            Write-Log "  Physical cores: $physicalCores"
            Write-Log "  Logical cores: $logicalCores"
            Write-Log ""
            Write-Log "To disable Hyper-Threading/SMT:"
            Write-Log "  1. Restart computer and enter BIOS/UEFI (usually F2, F10, DEL during boot)"
            Write-Log "  2. Navigate to: Advanced → CPU Configuration → Hyper-Threading (Intel) or SMT (AMD)"
            Write-Log "  3. Set to: Disabled"
            Write-Log "  4. Save and exit BIOS/UEFI"
            Write-Log "  5. Restart computer"
            Write-Log ""
            Write-Log "NOTE: This change cannot be applied programmatically - BIOS/UEFI access required."
            Write-Log "NOTE: To re-enable, repeat the process and set to Enabled."
            
            Add-Change -Ctx $ctx -Category "hyperthreading" -Key "recommendation" -Before "Enabled" -After "Disable via BIOS/UEFI" -Note "User opted to disable Hyper-Threading/SMT (requires manual BIOS change)"
        }
    } catch {
        Write-Log -Level "WARN" -Message "Hyper-Threading detection failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "Hyper-Threading/SMT management skipped (user opted out)"
}

# CPU C-States Diagnostics (Zero-Level Optimization)
if ($DiagnoseCStates -eq "true") {
    Write-Log "================================================================================"
    Write-Log "CPU C-States Diagnostics (Zero-Level Optimization)"
    Write-Log "================================================================================"
    Write-Log "C-States are CPU power-saving states. Disabling deeper C-States (C3, C6, C7) can"
    Write-Log "reduce latency transitions and improve responsiveness, but increases power consumption."
    Write-Log ""
    
    try {
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        Write-Log "CPU: $($cpu.Name)"
        Write-Log "Current C-States status: Cannot be determined programmatically (requires BIOS/UEFI inspection)"
        Write-Log ""
        Write-Log "RECOMMENDATION for competitive gaming:"
        Write-Log "  - Disable C3, C6, C7 states (keep C1/C1E enabled for basic power saving)"
        Write-Log "  - This reduces latency spikes from C-State transitions"
        Write-Log "  - Trade-off: Increased power consumption and heat"
        Write-Log ""
        Write-Log "To adjust C-States:"
        Write-Log "  1. Restart computer and enter BIOS/UEFI"
        Write-Log "  2. Navigate to: Advanced → CPU Configuration → CPU C-States"
        Write-Log "  3. Set C3, C6, C7 to: Disabled"
        Write-Log "  4. Keep C1/C1E: Enabled (optional, for basic power saving)"
        Write-Log "  5. Save and exit BIOS/UEFI"
        Write-Log "  6. Restart computer"
        Write-Log ""
        Write-Log "NOTE: This change cannot be applied programmatically - BIOS/UEFI access required."
        Write-Log "NOTE: Monitor temperatures after disabling C-States as CPU will consume more power."
        
        Add-Change -Ctx $ctx -Category "cstates" -Key "recommendation" -Before "Unknown" -After "Disable C3/C6/C7 via BIOS/UEFI" -Note "C-States diagnostic and recommendation provided"
    } catch {
        Write-Log -Level "WARN" -Message "C-States diagnostics failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "C-States diagnostics skipped (user opted out)"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


