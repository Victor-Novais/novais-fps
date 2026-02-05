param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = "",
    [ValidateSet("true","false")][string]$EnableInterruptSteering = "false"
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

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


