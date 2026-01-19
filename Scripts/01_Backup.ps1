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

$backupDir = Join-Path $WorkspaceRoot "Backup"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

if ($Mode -eq "Rollback") {
    Write-Log "01_Backup rollback: nothing to rollback here (restore point/reg export are safety artifacts)."
    exit 0
}

$ctx = Load-JsonFile -Path $ContextJson
if ($null -eq $ctx) { $ctx = [pscustomobject]@{} }

Write-Log "Creating system restore point (Checkpoint-Computer)..."
try {
    
    Checkpoint-Computer -Description "NOVAIS FPS ($RunId)" -RestorePointType "MODIFY_SETTINGS" | Out-Null
    Add-Change -Ctx $ctx -Category "backup" -Key "restorePoint" -Before $null -After "Created" -Note "Checkpoint-Computer"
    Write-Log "Restore point created."
} catch {
    Write-Log -Level "WARN" -Message "Restore point failed (may be disabled by policy): $($_.Exception.Message)"
    Add-Change -Ctx $ctx -Category "backup" -Key "restorePoint" -Before $null -After "Failed" -Note $_.Exception.Message
}

Write-Log "Exporting registry snapshot (HKLM + HKCU selected keys)..."
try {
    # Export a targeted set to avoid huge files; enough for our changes.
    $paths = @(
        "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
        "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
        "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
        "HKCU\Software\Microsoft\GameBar",
        "HKCU\System\GameConfigStore"
    )
    $exports = @()
    foreach ($p in $paths) {
        $safe = ($p -replace "[\\/:*?\""<>\| ]", "_")
        $out = Join-Path $backupDir ("reg-{0}-{1}.reg" -f $RunId, $safe)
        & reg.exe export $p $out /y | Out-Null
        $exports += $out
    }
    Add-Change -Ctx $ctx -Category "backup" -Key "regExports" -Before $null -After $exports -Note "reg.exe export (targeted, per-key)"
    Write-Log "Registry exports saved: $($exports.Count) files under Backup\\"
} catch {
    Write-Log -Level "WARN" -Message "Registry export failed: $($_.Exception.Message)"
}

Save-JsonFile -Obj $ctx -Path $ContextJson
exit 0


