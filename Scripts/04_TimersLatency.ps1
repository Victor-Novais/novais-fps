param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = "",
    [ValidateSet("true","false")][string]$EnableBcdTweaks = "false"
)

. (Join-Path $PSScriptRoot "_Common.ps1")
$script:LogFile = $LogFile

function Get-BcdValue {
    param([Parameter(Mandatory=$true)][string]$Name)
    try {
        $out = (bcdedit /enum "{current}") 2>$null
        $line = ($out | Select-String -Pattern ("^\s*{0}\s" -f [regex]::Escape($Name))) | Select-Object -First 1
        if ($line) { return ($line.Line -split "\s+")[1] }
        return $null
    } catch { return $null }
}

$ctx = Load-JsonFile -Path $ContextJson
if ($null -eq $ctx) { $ctx = [pscustomobject]@{} }

if ($Mode -eq "Rollback") {
    if (-not $TargetContextJson) { Write-Log -Level "ERROR" -Message "Rollback requires -TargetContextJson"; exit 2 }
    $target = Load-JsonFile -Path $TargetContextJson
    if ($null -eq $target) { Write-Log -Level "ERROR" -Message "Cannot load target context: $TargetContextJson"; exit 2 }

    Write-Log "Rollback: removing bcdedit values we may have set (useplatformclock / useplatformtick)."
    try { & bcdedit /deletevalue useplatformclock | Out-Null } catch { }
    try { & bcdedit /deletevalue useplatformtick | Out-Null } catch { }
    Write-Log "Rollback: bcdedit values removed (if present). Reboot may be required."
    exit 0
}

Write-Log "TimersLatency Apply: safe timer recommendations + optional bcdedit changes."

Write-Log "Current bcdedit values (best-effort):"
Write-Log ("  useplatformclock=" + (Get-BcdValue "useplatformclock"))
Write-Log ("  useplatformtick=" + (Get-BcdValue "useplatformtick"))

if ($EnableBcdTweaks -eq "true") {
    Write-Log "Applying bcdedit: useplatformclock=false (prefer TSC), useplatformtick=yes (reduce tick jitter on some systems)."
    $beforeClock = Get-BcdValue "useplatformclock"
    $beforeTick  = Get-BcdValue "useplatformtick"
    try {
        & bcdedit /set useplatformclock false | Out-Null
        & bcdedit /set useplatformtick yes | Out-Null
        $afterClock = Get-BcdValue "useplatformclock"
        $afterTick  = Get-BcdValue "useplatformtick"
        Add-Change -Ctx $ctx -Category "bcdedit" -Key "useplatformclock" -Before $beforeClock -After $afterClock -Note "Prefer TSC over HPET (reversible)"
        Add-Change -Ctx $ctx -Category "bcdedit" -Key "useplatformtick" -Before $beforeTick -After $afterTick -Note "Platform tick enabled (reversible)"
        Write-Log "BCDEdit applied. A reboot may be required."
    } catch {
        Write-Log -Level "WARN" -Message "BCDEdit failed: $($_.Exception.Message)"
    }
} else {
    Write-Log "BCDEdit not applied (user opted out)."
    Add-Change -Ctx $ctx -Category "bcdedit" -Key "optOut" -Before $null -After "true" -Note "User opted out of bcdedit"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


