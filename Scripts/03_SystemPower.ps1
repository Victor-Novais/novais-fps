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

function Get-ActivePowerSchemeGuid {
    $out = (powercfg /getactivescheme) 2>$null
    $m = [regex]::Match($out, "[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}")
    if ($m.Success) { return $m.Value }
    return ""
}

function Set-PowerSubValue {
    param(
        [Parameter(Mandatory=$true)][string]$Scheme,
        [Parameter(Mandatory=$true)][string]$Sub,
        [Parameter(Mandatory=$true)][string]$Setting,
        [Parameter(Mandatory=$true)][string]$AcValue,
        [Parameter(Mandatory=$true)][string]$DcValue,
        [Parameter(Mandatory=$true)][string]$KeyName
    )
    $before = @{
        ac = (powercfg /q $Scheme $Sub $Setting | Select-String -Pattern "Current AC Power Setting Index" -SimpleMatch | ForEach-Object { $_.Line.Trim() }) -join "`n"
        dc = (powercfg /q $Scheme $Sub $Setting | Select-String -Pattern "Current DC Power Setting Index" -SimpleMatch | ForEach-Object { $_.Line.Trim() }) -join "`n"
    }
    Run-Cmd -File "powercfg" -Args "/setacvalueindex $Scheme $Sub $Setting $AcValue" | Out-Null
    Run-Cmd -File "powercfg" -Args "/setdcvalueindex $Scheme $Sub $Setting $DcValue" | Out-Null
    Run-Cmd -File "powercfg" -Args "/S $Scheme" | Out-Null
    $after = @{
        ac = (powercfg /q $Scheme $Sub $Setting | Select-String -Pattern "Current AC Power Setting Index" -SimpleMatch | ForEach-Object { $_.Line.Trim() }) -join "`n"
        dc = (powercfg /q $Scheme $Sub $Setting | Select-String -Pattern "Current DC Power Setting Index" -SimpleMatch | ForEach-Object { $_.Line.Trim() }) -join "`n"
    }
    Add-Change -Ctx $ctx -Category "powercfg" -Key $KeyName -Before $before -After $after -Note "powercfg set value"
}

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }

    # Rollback: restore active power scheme if recorded
    $prev = $null
    if ($target.PSObject.Properties.Name -contains "changes") {
        $prev = @($target.changes) | Where-Object { $_.category -eq "powercfg" -and $_.key -eq "activeScheme" } | Select-Object -Last 1
    }
    if ($prev -and $prev.before -and $prev.before.guid) {
        Write-Log "Restoring previous power scheme: $($prev.before.guid)"
        Run-Cmd -File "powercfg" -Args "/S $($prev.before.guid)" | Out-Null
    } else {
        Write-Log -Level "WARN" -Message "No prior power scheme recorded; skipping scheme restore."
    }
    # Rollback services (whitelist)
    Rollback-ServicesFromChanges -TargetCtx $target -ServiceNames @("DiagTrack","dmwappushservice")

    # Rollback telemetry policy key (if present)
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"

    Write-Log "Rollback power/services/telemetry: best-effort. Restore point covers full rollback if needed."
    exit 0
}

Write-Log "SystemPower Apply: configuring power plan + safe latency-related settings."

$activeBefore = Get-ActivePowerSchemeGuid
Add-Change -Ctx $ctx -Category "powercfg" -Key "activeScheme" -Before @{ guid = $activeBefore } -After @{ guid = $activeBefore } -Note "captured pre-change"

# Reduce telemetry (safe policy knob; may be limited by Windows edition)
try {
    Set-RegistryDword -Ctx $ctx -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Name "AllowTelemetry" -Value 1 -Note "Reduce telemetry to Basic (where supported)"
    Write-Log "Telemetry policy: AllowTelemetry=1 (Basic) (where supported)."
} catch {
    Write-Log -Level "WARN" -Message "Telemetry policy tweak failed: $($_.Exception.Message)"
}

# Disable non-essential telemetry services (whitelist, reversible)
Set-ServiceSafe -Ctx $ctx -Name "DiagTrack" -StartupType "Disabled" -Action "Stop" -Note "Connected User Experiences and Telemetry"
Set-ServiceSafe -Ctx $ctx -Name "dmwappushservice" -StartupType "Disabled" -Action "Stop" -Note "WAP Push Message Routing (telemetry-related)"

# Ultimate Performance GUID (built-in in Win10+ but may not exist until duplicated)
$ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61"
try {
    $schemes = (powercfg /L) 2>$null
    if ($schemes -notmatch $ultimate) {
        Write-Log "Ultimate Performance not present; duplicating built-in scheme..."
        Run-Cmd -File "powercfg" -Args "/duplicatescheme $ultimate" | Out-Null
    }
    Write-Log "Activating Ultimate Performance..."
    Run-Cmd -File "powercfg" -Args "/S $ultimate" | Out-Null
    Add-Change -Ctx $ctx -Category "powercfg" -Key "activeScheme" -Before @{ guid = $activeBefore } -After @{ guid = $ultimate } -Note "set ultimate performance"
} catch {
    Write-Log -Level "WARN" -Message "Failed to set Ultimate Performance: $($_.Exception.Message)"
}

# Disable USB selective suspend (safe)
# SUB_USB = 2a737441-1930-4402-8d77-b2bebba308a3
# USBSELECTIVE = 48e6b7a6-50f5-4782-a5d4-53bb8f07e226
try {
    $scheme = Get-ActivePowerSchemeGuid
    if ($scheme) {
        Set-PowerSubValue -Scheme $scheme `
            -Sub "2a737441-1930-4402-8d77-b2bebba308a3" `
            -Setting "48e6b7a6-50f5-4782-a5d4-53bb8f07e226" `
            -AcValue "0" -DcValue "0" -KeyName "usbSelectiveSuspend"
        Write-Log "USB selective suspend: Disabled (AC/DC)."
    }
} catch {
    Write-Log -Level "WARN" -Message "USB selective suspend tweak failed: $($_.Exception.Message)"
}

# Disable PCIe Link State Power Management (safe for latency)
# SUB_PCIEXPRESS = 501a4d13-42af-4429-9fd1-a8218c268e20
# ASPM = ee12f906-d277-404b-b6da-e5fa1a576df5 (0=off)
try {
    $scheme = Get-ActivePowerSchemeGuid
    if ($scheme) {
        Set-PowerSubValue -Scheme $scheme `
            -Sub "501a4d13-42af-4429-9fd1-a8218c268e20" `
            -Setting "ee12f906-d277-404b-b6da-e5fa1a576df5" `
            -AcValue "0" -DcValue "0" -KeyName "pcieLinkState"
        Write-Log "PCIe Link State Power Management: Off (AC/DC)."
    }
} catch {
    Write-Log -Level "WARN" -Message "PCIe ASPM tweak failed: $($_.Exception.Message)"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


