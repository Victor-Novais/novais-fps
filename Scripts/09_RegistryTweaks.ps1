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

$sysProfile = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
$gamesTask  = Join-Path $sysProfile "Tasks\Games"

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }
    Write-Log "Rollback RegistryTweaks (best-effort)."
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch $sysProfile
    Rollback-RegistryFromChanges -TargetCtx $target -PrefixMatch "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"
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

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


