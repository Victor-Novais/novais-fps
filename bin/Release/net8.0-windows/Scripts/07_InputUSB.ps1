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

function Apply-GlobalUsbSelectiveSuspendOff {
    # HKLM\SYSTEM\CurrentControlSet\Services\USB\Parameters\SelectiveSuspendEnabled = 0
    $path = "HKLM:\SYSTEM\CurrentControlSet\Services\USB\Parameters"
    try {
        Set-RegistryDword -Ctx $ctx -Path $path -Name "SelectiveSuspendEnabled" -Value 0 -Note "Global USB selective suspend off"
        Write-Log "Global USB SelectiveSuspendEnabled=0 set."
    } catch {
        Write-Log -Level "WARN" -Message "Failed to set global SelectiveSuspendEnabled: $($_.Exception.Message)"
    }
}

function Apply-UsbHostPowerOff {
    # Best-effort: disable "allow computer to turn off this device" for USB host controllers
    # by toggling common idle/power settings under Enum\PCI for USB controllers.
    $basePci = "HKLM:\SYSTEM\CurrentControlSet\Enum\PCI"
    if (-not (Test-Path $basePci)) { return }

    $hostPaths = Get-ChildItem -Path $basePci -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match "VEN_" -and $_.PSPath -like "*\Device Parameters" -and $_.PSPath -like "*USB*" }

    $count = 0
    foreach ($p in $hostPaths) {
        try {
            $before1 = Get-RegistryValueSafe -Path $p.PSPath -Name "IdleInWorkingState"
            $before2 = Get-RegistryValueSafe -Path $p.PSPath -Name "DeviceSelectiveSuspended"
            if ($null -ne $before1 -or $null -ne $before2) {
                Set-ItemProperty -Path $p.PSPath -Name "IdleInWorkingState" -Type DWord -Value 0 -Force
                Set-ItemProperty -Path $p.PSPath -Name "DeviceSelectiveSuspended" -Type DWord -Value 0 -Force
                $after1 = Get-RegistryValueSafe -Path $p.PSPath -Name "IdleInWorkingState"
                $after2 = Get-RegistryValueSafe -Path $p.PSPath -Name "DeviceSelectiveSuspended"
                if ($null -ne $before1) {
                    Add-Change -Ctx $ctx -Category "registry" -Key "$($p.PSPath)\IdleInWorkingState" -Before $before1 -After $after1 -Note "USB host power idle off"
                }
                if ($null -ne $before2) {
                    Add-Change -Ctx $ctx -Category "registry" -Key "$($p.PSPath)\DeviceSelectiveSuspended" -Before $before2 -After $after2 -Note "USB host selective suspend off"
                }
                $count++
            }
        } catch { }
    }
    Write-Log "InputUSB: host controller power-saving disabled on $count locations (best-effort)."
}

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }
    Write-Log "Rollback Input/USB registry changes (best-effort)."
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKLM:\SYSTEM\CurrentControlSet\Enum\USB"
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKLM:\SYSTEM\CurrentControlSet\Services\USB\Parameters"
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKLM:\SYSTEM\CurrentControlSet\Enum\PCI"
    exit 0
}

Write-Log "InputUSB Apply: prevent HID/USB power saving behaviors (safe, reversible)."
Apply-EnhancedPowerMgmtOff
Apply-GlobalUsbSelectiveSuspendOff
Apply-UsbHostPowerOff

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0



