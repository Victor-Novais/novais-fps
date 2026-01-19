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

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


