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

$ctx = Load-JsonFile -Path $ContextJson
if ($null -eq $ctx) { $ctx = [pscustomobject]@{} }

function Apply-EnhancedPowerMgmtOff {
    # Best-effort: disable EnhancedPowerManagementEnabled where present (common cause of HID sleep/latency spikes)
    $base = "HKLM:\SYSTEM\CurrentControlSet\Enum\USB"
    if (-not (Test-Path $base)) { return }

    $paths = Get-ChildItem -Path $base -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.PSPath -like "*\Device Parameters" }

    $count = 0
    foreach ($p in $paths) {
        try {
            $before = Get-RegistryValueSafe -Path $p.PSPath -Name "EnhancedPowerManagementEnabled"
            if ($null -ne $before) {
                Set-ItemProperty -Path $p.PSPath -Name "EnhancedPowerManagementEnabled" -Type DWord -Value 0 -Force
                $after = Get-RegistryValueSafe -Path $p.PSPath -Name "EnhancedPowerManagementEnabled"
                Add-Change -Ctx $ctx -Category "registry" -Key "$($p.PSPath)\EnhancedPowerManagementEnabled" -Before $before -After $after -Note "Disable EnhancedPowerManagementEnabled for USB device"
                $count++
            }
        } catch { }
    }
    Write-Log "InputUSB: EnhancedPowerManagementEnabled set to 0 on $count keys (where present)."
}

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }
    Write-Log "Rollback Input/USB registry changes (best-effort)."
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKLM:\SYSTEM\CurrentControlSet\Enum\USB"
    exit 0
}

Write-Log "InputUSB Apply: prevent HID/USB power saving behaviors (safe, reversible)."
Apply-EnhancedPowerMgmtOff

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0



